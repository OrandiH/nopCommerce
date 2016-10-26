﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Core.Domain.Vendors;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.ExportImport.Help;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Seo;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Services.Vendors;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace Nop.Services.ExportImport
{
    /// <summary>
    /// Export manager
    /// </summary>
    public partial class ExportManager : IExportManager
    {
        #region Fields

        private readonly ICategoryService _categoryService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IProductAttributeService _productAttributeService;
        private readonly IPictureService _pictureService;
        private readonly INewsLetterSubscriptionService _newsLetterSubscriptionService;
        private readonly IStoreService _storeService;
        private readonly IWorkContext _workContext;
        private readonly ProductEditorSettings _productEditorSettings;
        private readonly IVendorService _vendorService;
        private readonly IProductTemplateService _productTemplateService;
        private readonly IShippingService _shippingService;
        private readonly ITaxCategoryService _taxCategoryService;
        private readonly IMeasureService _measureService;
        private readonly CatalogSettings _catalogSettings;

        #endregion

        #region Ctor

        public ExportManager(ICategoryService categoryService,
            IManufacturerService manufacturerService,
            IProductAttributeService productAttributeService,
            IPictureService pictureService,
            INewsLetterSubscriptionService newsLetterSubscriptionService,
            IStoreService storeService,
            IWorkContext workContext,
            ProductEditorSettings productEditorSettings,
            IVendorService vendorService,
            IProductTemplateService productTemplateService,
            IShippingService shippingService,
            ITaxCategoryService taxCategoryService,
            IMeasureService measureService,
            CatalogSettings catalogSettings)
        {
            this._categoryService = categoryService;
            this._manufacturerService = manufacturerService;
            this._productAttributeService = productAttributeService;
            this._pictureService = pictureService;
            this._newsLetterSubscriptionService = newsLetterSubscriptionService;
            this._storeService = storeService;
            this._workContext = workContext;
            this._productEditorSettings = productEditorSettings;
            this._vendorService = vendorService;
            this._productTemplateService = productTemplateService;
            this._shippingService = shippingService;
            this._taxCategoryService = taxCategoryService;
            this._measureService = measureService;
            this._catalogSettings = catalogSettings;
        }

        #endregion

        #region Utilities

        protected virtual void WriteCategories(XmlWriter xmlWriter, int parentCategoryId)
        {
            var categories = _categoryService.GetAllCategoriesByParentCategoryId(parentCategoryId, true);
            if (categories != null && categories.Any())
            {
                foreach (var category in categories)
                {
                    xmlWriter.WriteStartElement("Category");
                    xmlWriter.WriteElementString("Id", null, category.Id.ToString());
                    xmlWriter.WriteElementString("Name", null, category.Name);
                    xmlWriter.WriteElementString("Description", null, category.Description);
                    xmlWriter.WriteElementString("CategoryTemplateId", null, category.CategoryTemplateId.ToString());
                    xmlWriter.WriteElementString("MetaKeywords", null, category.MetaKeywords);
                    xmlWriter.WriteElementString("MetaDescription", null, category.MetaDescription);
                    xmlWriter.WriteElementString("MetaTitle", null, category.MetaTitle);
                    xmlWriter.WriteElementString("SeName", null, category.GetSeName(0));
                    xmlWriter.WriteElementString("ParentCategoryId", null, category.ParentCategoryId.ToString());
                    xmlWriter.WriteElementString("PictureId", null, category.PictureId.ToString());
                    xmlWriter.WriteElementString("PageSize", null, category.PageSize.ToString());
                    xmlWriter.WriteElementString("AllowCustomersToSelectPageSize", null, category.AllowCustomersToSelectPageSize.ToString());
                    xmlWriter.WriteElementString("PageSizeOptions", null, category.PageSizeOptions);
                    xmlWriter.WriteElementString("PriceRanges", null, category.PriceRanges);
                    xmlWriter.WriteElementString("ShowOnHomePage", null, category.ShowOnHomePage.ToString());
                    xmlWriter.WriteElementString("IncludeInTopMenu", null, category.IncludeInTopMenu.ToString());
                    xmlWriter.WriteElementString("Published", null, category.Published.ToString());
                    xmlWriter.WriteElementString("Deleted", null, category.Deleted.ToString());
                    xmlWriter.WriteElementString("DisplayOrder", null, category.DisplayOrder.ToString());
                    xmlWriter.WriteElementString("CreatedOnUtc", null, category.CreatedOnUtc.ToString());
                    xmlWriter.WriteElementString("UpdatedOnUtc", null, category.UpdatedOnUtc.ToString());

                    xmlWriter.WriteStartElement("Products");
                    var productCategories = _categoryService.GetProductCategoriesByCategoryId(category.Id, showHidden: true);
                    foreach (var productCategory in productCategories)
                    {
                        var product = productCategory.Product;
                        if (product != null && !product.Deleted)
                        {
                            xmlWriter.WriteStartElement("ProductCategory");
                            xmlWriter.WriteElementString("ProductCategoryId", null, productCategory.Id.ToString());
                            xmlWriter.WriteElementString("ProductId", null, productCategory.ProductId.ToString());
                            xmlWriter.WriteElementString("ProductName", null, product.Name);
                            xmlWriter.WriteElementString("IsFeaturedProduct", null, productCategory.IsFeaturedProduct.ToString());
                            xmlWriter.WriteElementString("DisplayOrder", null, productCategory.DisplayOrder.ToString());
                            xmlWriter.WriteEndElement();
                        }
                    }
                    xmlWriter.WriteEndElement();

                    xmlWriter.WriteStartElement("SubCategories");
                    WriteCategories(xmlWriter, category.Id);
                    xmlWriter.WriteEndElement();
                    xmlWriter.WriteEndElement();
                }
            }
        }

        protected virtual void SetCaptionStyle(ExcelStyle style)
        {
            style.Fill.PatternType = ExcelFillStyle.Solid;
            style.Fill.BackgroundColor.SetColor(Color.FromArgb(184, 204, 228));
            style.Font.Bold = true;
        }

        /// <summary>
        /// Returns the path to the image file by ID
        /// </summary>
        /// <param name="pictureId">Picture ID</param>
        /// <returns>Path to the image file</returns>
        protected virtual string GetPictures(int pictureId)
        {
            var picture = _pictureService.GetPictureById(pictureId);
            return _pictureService.GetThumbLocalPath(picture);
        }

        /// <summary>
        /// Returns the list of categories for a product separated by a ";"
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>List of categories</returns>
        protected virtual string GetCategories(Product product)
        {
            string categoryNames = null;
            foreach (var pc in _categoryService.GetProductCategoriesByProductId(product.Id, true))
            {
                categoryNames += pc.Category.Name;
                categoryNames += ";";
            }
            return categoryNames;
        }

        /// <summary>
        /// Returns the list of manufacturer for a product separated by a ";"
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>List of manufacturer</returns>
        protected virtual string GetManufacturers(Product product)
        {
            string manufacturerNames = null;
            foreach (var pm in _manufacturerService.GetProductManufacturersByProductId(product.Id, true))
            {
                manufacturerNames += pm.Manufacturer.Name;
                manufacturerNames += ";";
            }
            return manufacturerNames;
        }

        /// <summary>
        /// Returns the list of product tag for a product separated by a ";"
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>List of product tag</returns>
        protected virtual string GetProductTags(Product product)
        {
            string productTagNames = null;

            foreach (var productTag in product.ProductTags)
            {
                productTagNames += productTag.Name;
                productTagNames += ";";
            }
            return productTagNames;
        }

        /// <summary>
        /// Returns the three first image associated with the product
        /// </summary>
        /// <param name="product">Product</param>
        /// <returns>three first image</returns>
        protected virtual string[] GetPictures(Product product)
        {
            //pictures (up to 3 pictures)
            string picture1 = null;
            string picture2 = null;
            string picture3 = null;
            var pictures = _pictureService.GetPicturesByProductId(product.Id, 3);
            for (var i = 0; i < pictures.Count; i++)
            {
                var pictureLocalPath = _pictureService.GetThumbLocalPath(pictures[i]);
                switch (i)
                {
                    case 0:
                        picture1 = pictureLocalPath;
                        break;
                    case 1:
                        picture2 = pictureLocalPath;
                        break;
                    case 2:
                        picture3 = pictureLocalPath;
                        break;
                }
            }
            return new[] { picture1, picture2, picture3 };
        }

        private bool IgnoreExportPoductProperty(Func<ProductEditorSettings, bool> func)
        {
            var productAdvancedMode = _workContext.CurrentCustomer.GetAttribute<bool>("product-advanced-mode");
            return !productAdvancedMode && !func(_productEditorSettings);
        }
        
        /// <summary>
        /// Export objects to XLSX
        /// </summary>
        /// <typeparam name="T">Type of object</typeparam>
        /// <param name="properties">Class access to the object through its properties</param>
        /// <param name="itemsToExport">The objects to export</param>
        /// <returns></returns>
        protected virtual byte[] ExportToXlsx<T>(PropertyByName<T>[] properties, IEnumerable<T> itemsToExport)
        {
            using (var stream = new MemoryStream())
            {
                // ok, we can run the real code of the sample now
                using (var xlPackage = new ExcelPackage(stream))
                {
                    // uncomment this line if you want the XML written out to the outputDir
                    //xlPackage.DebugMode = true; 

                    // get handles to the worksheets
                    var worksheet = xlPackage.Workbook.Worksheets.Add(typeof(T).Name);
                    var fWorksheet = xlPackage.Workbook.Worksheets.Add("DataForProductsFilters");
                    fWorksheet.Hidden = eWorkSheetHidden.VeryHidden;
                    
                    //create Headers and format them 
                    var manager = new PropertyManager<T>(properties.Where(p => !p.Ignore).ToArray());
                    manager.WriteCaption(worksheet, SetCaptionStyle);

                    var row = 2;
                    foreach (var items in itemsToExport)
                    {
                        manager.CurrentObject = items;
                        manager.WriteToXlsx(worksheet, row++, _catalogSettings.ExportImportUseDropdownlistsForAssociatedEntities, fWorksheet: fWorksheet);
                    }

                    xlPackage.Save();
                }
                return stream.ToArray();
            }
        }

        private byte[] ExportProductsToXlsxWithAttributes(PropertyByName<Product>[] properties, IEnumerable<Product> itemsToExport)
        {
            var attributeProperties = new[]
            {
                new PropertyByName<ExportProductAttribute>("AttributeId", p => p.AttributeId),
                new PropertyByName<ExportProductAttribute>("AttributeName", p => p.AttributeName),
                new PropertyByName<ExportProductAttribute>("AttributeTextPrompt", p => p.AttributeTextPrompt),
                new PropertyByName<ExportProductAttribute>("AttributeIsRequired", p => p.AttributeIsRequired),
                new PropertyByName<ExportProductAttribute>("AttributeControlType", p => p.AttributeControlTypeId)
                {
                    DropDownElements = AttributeControlType.TextBox.ToSelectList(useLocalization: false)
                },
                new PropertyByName<ExportProductAttribute>("AttributeDisplayOrder", p => p.AttributeDisplayOrder),
                new PropertyByName<ExportProductAttribute>("ProductAttributeValueId", p => p.Id),
                new PropertyByName<ExportProductAttribute>("ValueName", p => p.Name),
                new PropertyByName<ExportProductAttribute>("AttributeValueType", p => p.AttributeValueTypeId)
                {
                    DropDownElements = AttributeValueType.Simple.ToSelectList(useLocalization: false)
                },
                new PropertyByName<ExportProductAttribute>("AssociatedProductId", p => p.AssociatedProductId),
                new PropertyByName<ExportProductAttribute>("ColorSquaresRgb", p => p.ColorSquaresRgb),
                new PropertyByName<ExportProductAttribute>("ImageSquaresPictureId", p => p.ImageSquaresPictureId),
                new PropertyByName<ExportProductAttribute>("PriceAdjustment", p => p.PriceAdjustment),
                new PropertyByName<ExportProductAttribute>("WeightAdjustment", p => p.WeightAdjustment),
                new PropertyByName<ExportProductAttribute>("Cost", p => p.Cost),
                new PropertyByName<ExportProductAttribute>("CustomerEntersQty", p => p.CustomerEntersQty),
                new PropertyByName<ExportProductAttribute>("Quantity", p => p.Quantity),
                new PropertyByName<ExportProductAttribute>("IsPreSelected", p => p.IsPreSelected),
                new PropertyByName<ExportProductAttribute>("DisplayOrder", p => p.DisplayOrder),
                new PropertyByName<ExportProductAttribute>("PictureId", p => p.PictureId)
            };

            var attributeManager = new PropertyManager<ExportProductAttribute>(attributeProperties);

            using (var stream = new MemoryStream())
            {
                // ok, we can run the real code of the sample now
                using (var xlPackage = new ExcelPackage(stream))
                {
                    // uncomment this line if you want the XML written out to the outputDir
                    //xlPackage.DebugMode = true; 

                    // get handles to the worksheets
                    var worksheet = xlPackage.Workbook.Worksheets.Add(typeof(Product).Name);
                    var fpWorksheet = xlPackage.Workbook.Worksheets.Add("DataForProductsFilters");
                    fpWorksheet.Hidden=eWorkSheetHidden.VeryHidden;
                    var faWorksheet = xlPackage.Workbook.Worksheets.Add("DataForProductAttributesFilters");
                    faWorksheet.Hidden = eWorkSheetHidden.VeryHidden;

                    //create Headers and format them 
                    var manager = new PropertyManager<Product>(properties.Where(p => !p.Ignore).ToArray());
                    manager.WriteCaption(worksheet, SetCaptionStyle);

                    var row = 2;
                    foreach (var item in itemsToExport)
                    {
                        manager.CurrentObject = item;
                        manager.WriteToXlsx(worksheet, row++, _catalogSettings.ExportImportUseDropdownlistsForAssociatedEntities, fWorksheet: fpWorksheet);

                        var attributes = item.ProductAttributeMappings.SelectMany(pam => pam.ProductAttributeValues.Select(pav => new ExportProductAttribute
                        {
                            AttributeId = pam.ProductAttribute.Id,
                            AttributeName = pam.ProductAttribute.Name,
                            AttributeTextPrompt = pam.TextPrompt,
                            AttributeIsRequired = pam.IsRequired,
                            AttributeControlTypeId = pam.AttributeControlTypeId,
                            AssociatedProductId = pav.AssociatedProductId,
                            AttributeDisplayOrder = pam.DisplayOrder,
                            Id = pav.Id,
                            Name = pav.Name,
                            AttributeValueTypeId = pav.AttributeValueTypeId,
                            ColorSquaresRgb = pav.ColorSquaresRgb,
                            ImageSquaresPictureId = pav.ImageSquaresPictureId,
                            PriceAdjustment = pav.PriceAdjustment,
                            WeightAdjustment = pav.WeightAdjustment,
                            Cost = pav.Cost,
                            CustomerEntersQty = pav.CustomerEntersQty,
                            Quantity = pav.Quantity,
                            IsPreSelected = pav.IsPreSelected,
                            DisplayOrder = pav.DisplayOrder,
                            PictureId = pav.PictureId
                        })).ToList();

                        attributes.AddRange(item.ProductAttributeMappings.Where(pam => !pam.ProductAttributeValues.Any()).Select(pam => new ExportProductAttribute
                        {
                            AttributeId = pam.ProductAttribute.Id,
                            AttributeName = pam.ProductAttribute.Name,
                            AttributeTextPrompt = pam.TextPrompt,
                            AttributeIsRequired = pam.IsRequired,
                            AttributeControlTypeId = pam.AttributeControlTypeId
                        }));

                        if (!attributes.Any())
                            continue;

                        attributeManager.WriteCaption(worksheet, SetCaptionStyle, row, ExportProductAttribute.ProducAttributeCellOffset);
                        worksheet.Row(row).OutlineLevel = 1;
                        worksheet.Row(row).Collapsed = true;

                        foreach (var exportProducAttribute in attributes)
                        {
                            row++;
                            attributeManager.CurrentObject = exportProducAttribute;
                            attributeManager.WriteToXlsx(worksheet, row, _catalogSettings.ExportImportUseDropdownlistsForAssociatedEntities, ExportProductAttribute.ProducAttributeCellOffset, faWorksheet);
                            worksheet.Row(row).OutlineLevel = 1;
                            worksheet.Row(row).Collapsed = true;
                        }

                        row++;
                    }

                    xlPackage.Save();
                }
                return stream.ToArray();
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// Export manufacturer list to xml
        /// </summary>
        /// <param name="manufacturers">Manufacturers</param>
        /// <returns>Result in XML format</returns>
        public virtual string ExportManufacturersToXml(IList<Manufacturer> manufacturers)
        {
            var sb = new StringBuilder();
            var stringWriter = new StringWriter(sb);
            var xmlWriter = new XmlTextWriter(stringWriter);
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("Manufacturers");
            xmlWriter.WriteAttributeString("Version", NopVersion.CurrentVersion);

            foreach (var manufacturer in manufacturers)
            {
                xmlWriter.WriteStartElement("Manufacturer");

                xmlWriter.WriteElementString("ManufacturerId", null, manufacturer.Id.ToString());
                xmlWriter.WriteElementString("Name", null, manufacturer.Name);
                xmlWriter.WriteElementString("Description", null, manufacturer.Description);
                xmlWriter.WriteElementString("ManufacturerTemplateId", null, manufacturer.ManufacturerTemplateId.ToString());
                xmlWriter.WriteElementString("MetaKeywords", null, manufacturer.MetaKeywords);
                xmlWriter.WriteElementString("MetaDescription", null, manufacturer.MetaDescription);
                xmlWriter.WriteElementString("MetaTitle", null, manufacturer.MetaTitle);
                xmlWriter.WriteElementString("SEName", null, manufacturer.GetSeName(0));
                xmlWriter.WriteElementString("PictureId", null, manufacturer.PictureId.ToString());
                xmlWriter.WriteElementString("PageSize", null, manufacturer.PageSize.ToString());
                xmlWriter.WriteElementString("AllowCustomersToSelectPageSize", null, manufacturer.AllowCustomersToSelectPageSize.ToString());
                xmlWriter.WriteElementString("PageSizeOptions", null, manufacturer.PageSizeOptions);
                xmlWriter.WriteElementString("PriceRanges", null, manufacturer.PriceRanges);
                xmlWriter.WriteElementString("Published", null, manufacturer.Published.ToString());
                xmlWriter.WriteElementString("Deleted", null, manufacturer.Deleted.ToString());
                xmlWriter.WriteElementString("DisplayOrder", null, manufacturer.DisplayOrder.ToString());
                xmlWriter.WriteElementString("CreatedOnUtc", null, manufacturer.CreatedOnUtc.ToString());
                xmlWriter.WriteElementString("UpdatedOnUtc", null, manufacturer.UpdatedOnUtc.ToString());

                xmlWriter.WriteStartElement("Products");
                var productManufacturers = _manufacturerService.GetProductManufacturersByManufacturerId(manufacturer.Id, showHidden: true);
                if (productManufacturers != null)
                {
                    foreach (var productManufacturer in productManufacturers)
                    {
                        var product = productManufacturer.Product;
                        if (product != null && !product.Deleted)
                        {
                            xmlWriter.WriteStartElement("ProductManufacturer");
                            xmlWriter.WriteElementString("ProductManufacturerId", null, productManufacturer.Id.ToString());
                            xmlWriter.WriteElementString("ProductId", null, productManufacturer.ProductId.ToString());
                            xmlWriter.WriteElementString("ProductName", null, product.Name);
                            xmlWriter.WriteElementString("IsFeaturedProduct", null, productManufacturer.IsFeaturedProduct.ToString());
                            xmlWriter.WriteElementString("DisplayOrder", null, productManufacturer.DisplayOrder.ToString());
                            xmlWriter.WriteEndElement();
                        }
                    }
                }
                xmlWriter.WriteEndElement();

                xmlWriter.WriteEndElement();
            }

            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
            xmlWriter.Close();
            return stringWriter.ToString();
        }

        /// <summary>
        /// Export manufacturers to XLSX
        /// </summary>
        /// <param name="manufacturers">Manufactures</param>
        public virtual byte[] ExportManufacturersToXlsx(IEnumerable<Manufacturer> manufacturers)
        {
            //property array
            var properties = new[]
            {
                new PropertyByName<Manufacturer>("Id", p => p.Id),
                new PropertyByName<Manufacturer>("Name", p => p.Name),
                new PropertyByName<Manufacturer>("Description", p => p.Description),
                new PropertyByName<Manufacturer>("ManufacturerTemplateId", p => p.ManufacturerTemplateId),
                new PropertyByName<Manufacturer>("MetaKeywords", p => p.MetaKeywords),
                new PropertyByName<Manufacturer>("MetaDescription", p => p.MetaDescription),
                new PropertyByName<Manufacturer>("MetaTitle", p => p.MetaTitle),
                new PropertyByName<Manufacturer>("SeName", p => p.GetSeName(0)),
                new PropertyByName<Manufacturer>("Picture", p => GetPictures(p.PictureId)),
                new PropertyByName<Manufacturer>("PageSize", p => p.PageSize),
                new PropertyByName<Manufacturer>("AllowCustomersToSelectPageSize", p => p.AllowCustomersToSelectPageSize),
                new PropertyByName<Manufacturer>("PageSizeOptions", p => p.PageSizeOptions),
                new PropertyByName<Manufacturer>("PriceRanges", p => p.PriceRanges),
                new PropertyByName<Manufacturer>("Published", p => p.Published),
                new PropertyByName<Manufacturer>("DisplayOrder", p => p.DisplayOrder)
            };

            return ExportToXlsx(properties, manufacturers);
        }

        /// <summary>
        /// Export category list to xml
        /// </summary>
        /// <returns>Result in XML format</returns>
        public virtual string ExportCategoriesToXml()
        {
            var sb = new StringBuilder();
            var stringWriter = new StringWriter(sb);
            var xmlWriter = new XmlTextWriter(stringWriter);
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("Categories");
            xmlWriter.WriteAttributeString("Version", NopVersion.CurrentVersion);
            WriteCategories(xmlWriter, 0);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
            xmlWriter.Close();
            return stringWriter.ToString();
        }

        /// <summary>
        /// Export categories to XLSX
        /// </summary>
        /// <param name="categories">Categories</param>
        public virtual byte[] ExportCategoriesToXlsx(IEnumerable<Category> categories)
        {
            //property array
            var properties = new[]
            {
                new PropertyByName<Category>("Id", p => p.Id),
                new PropertyByName<Category>("Name", p => p.Name),
                new PropertyByName<Category>("Description", p => p.Description),
                new PropertyByName<Category>("CategoryTemplateId", p => p.CategoryTemplateId),
                new PropertyByName<Category>("MetaKeywords", p => p.MetaKeywords),
                new PropertyByName<Category>("MetaDescription", p => p.MetaDescription),
                new PropertyByName<Category>("MetaTitle", p => p.MetaTitle),
                new PropertyByName<Category>("SeName", p => p.GetSeName(0)),
                new PropertyByName<Category>("ParentCategoryId", p => p.ParentCategoryId),
                new PropertyByName<Category>("Picture", p => GetPictures(p.PictureId)),
                new PropertyByName<Category>("PageSize", p => p.PageSize),
                new PropertyByName<Category>("AllowCustomersToSelectPageSize", p => p.AllowCustomersToSelectPageSize),
                new PropertyByName<Category>("PageSizeOptions", p => p.PageSizeOptions),
                new PropertyByName<Category>("PriceRanges", p => p.PriceRanges),
                new PropertyByName<Category>("ShowOnHomePage", p => p.ShowOnHomePage),
                new PropertyByName<Category>("IncludeInTopMenu", p => p.IncludeInTopMenu),
                new PropertyByName<Category>("Published", p => p.Published),
                new PropertyByName<Category>("DisplayOrder", p => p.DisplayOrder)
            };
            return ExportToXlsx(properties, categories);
        }

        /// <summary>
        /// Export product list to xml
        /// </summary>
        /// <param name="products">Products</param>
        /// <returns>Result in XML format</returns>
        public virtual string ExportProductsToXml(IList<Product> products)
        {
            var sb = new StringBuilder();
            var stringWriter = new StringWriter(sb);
            var xmlWriter = new XmlTextWriter(stringWriter);
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("Products");
            xmlWriter.WriteAttributeString("Version", NopVersion.CurrentVersion);

            foreach (var product in products)
            {
                xmlWriter.WriteStartElement("Product");

                xmlWriter.WriteString("ProductId", product.Id, IgnoreExportPoductProperty(p => p.Id));
                xmlWriter.WriteString("ProductTypeId", product.ProductTypeId, IgnoreExportPoductProperty(p => p.ProductType));
                xmlWriter.WriteString("ParentGroupedProductId", product.ParentGroupedProductId, IgnoreExportPoductProperty(p => p.ProductType));
                xmlWriter.WriteString("VisibleIndividually", product.VisibleIndividually, IgnoreExportPoductProperty(p => p.VisibleIndividually));
                xmlWriter.WriteString("Name", product.Name);
                xmlWriter.WriteString("ShortDescription", product.ShortDescription);
                xmlWriter.WriteString("FullDescription", product.FullDescription);
                xmlWriter.WriteString("AdminComment", product.AdminComment, IgnoreExportPoductProperty(p => p.AdminComment));
                xmlWriter.WriteString("VendorId", product.VendorId, IgnoreExportPoductProperty(p => p.Vendor));
                xmlWriter.WriteString("ProductTemplateId", product.ProductTemplateId, IgnoreExportPoductProperty(p => p.ProductTemplate));
                xmlWriter.WriteString("ShowOnHomePage", product.ShowOnHomePage, IgnoreExportPoductProperty(p => p.ShowOnHomePage));
                xmlWriter.WriteString("MetaKeywords", product.MetaKeywords, IgnoreExportPoductProperty(p => p.Seo));
                xmlWriter.WriteString("MetaDescription", product.MetaDescription, IgnoreExportPoductProperty(p => p.Seo));
                xmlWriter.WriteString("MetaTitle", product.MetaTitle, IgnoreExportPoductProperty(p => p.Seo));
                xmlWriter.WriteString("SEName", product.GetSeName(0), IgnoreExportPoductProperty(p => p.Seo));
                xmlWriter.WriteString("AllowCustomerReviews", product.AllowCustomerReviews, IgnoreExportPoductProperty(p => p.AllowCustomerReviews));
                xmlWriter.WriteString("SKU", product.Sku);
                xmlWriter.WriteString("ManufacturerPartNumber", product.ManufacturerPartNumber, IgnoreExportPoductProperty(p => p.ManufacturerPartNumber));
                xmlWriter.WriteString("Gtin", product.Gtin, IgnoreExportPoductProperty(p => p.GTIN));
                xmlWriter.WriteString("IsGiftCard", product.IsGiftCard, IgnoreExportPoductProperty(p => p.IsGiftCard));
                xmlWriter.WriteString("GiftCardType", product.GiftCardType, IgnoreExportPoductProperty(p => p.IsGiftCard));
                xmlWriter.WriteString("OverriddenGiftCardAmount", product.OverriddenGiftCardAmount, IgnoreExportPoductProperty(p => p.IsGiftCard));
                xmlWriter.WriteString("RequireOtherProducts", product.RequireOtherProducts, IgnoreExportPoductProperty(p => p.RequireOtherProductsAddedToTheCart));
                xmlWriter.WriteString("RequiredProductIds", product.RequiredProductIds, IgnoreExportPoductProperty(p => p.RequireOtherProductsAddedToTheCart));
                xmlWriter.WriteString("AutomaticallyAddRequiredProducts", product.AutomaticallyAddRequiredProducts, IgnoreExportPoductProperty(p => p.RequireOtherProductsAddedToTheCart));
                xmlWriter.WriteString("IsDownload", product.IsDownload, IgnoreExportPoductProperty(p => p.DownloadableProduct));
                xmlWriter.WriteString("DownloadId", product.DownloadId, IgnoreExportPoductProperty(p => p.DownloadableProduct));
                xmlWriter.WriteString("UnlimitedDownloads", product.UnlimitedDownloads, IgnoreExportPoductProperty(p => p.DownloadableProduct));
                xmlWriter.WriteString("MaxNumberOfDownloads", product.MaxNumberOfDownloads, IgnoreExportPoductProperty(p => p.DownloadableProduct));
                xmlWriter.WriteString("DownloadExpirationDays", product.DownloadExpirationDays, IgnoreExportPoductProperty(p => p.DownloadableProduct));
                xmlWriter.WriteString("DownloadActivationType", product.DownloadActivationType, IgnoreExportPoductProperty(p => p.DownloadableProduct));
                xmlWriter.WriteString("HasSampleDownload", product.HasSampleDownload, IgnoreExportPoductProperty(p => p.DownloadableProduct));
                xmlWriter.WriteString("SampleDownloadId", product.SampleDownloadId, IgnoreExportPoductProperty(p => p.DownloadableProduct));
                xmlWriter.WriteString("HasUserAgreement", product.HasUserAgreement, IgnoreExportPoductProperty(p => p.DownloadableProduct));
                xmlWriter.WriteString("UserAgreementText", product.UserAgreementText, IgnoreExportPoductProperty(p => p.DownloadableProduct));
                xmlWriter.WriteString("IsRecurring", product.IsRecurring, IgnoreExportPoductProperty(p => p.RecurringProduct));
                xmlWriter.WriteString("RecurringCycleLength", product.RecurringCycleLength, IgnoreExportPoductProperty(p => p.RecurringProduct));
                xmlWriter.WriteString("RecurringCyclePeriodId", product.RecurringCyclePeriodId, IgnoreExportPoductProperty(p => p.RecurringProduct));
                xmlWriter.WriteString("RecurringTotalCycles", product.RecurringTotalCycles, IgnoreExportPoductProperty(p => p.RecurringProduct));
                xmlWriter.WriteString("IsRental", product.IsRental, IgnoreExportPoductProperty(p => p.IsRental));
                xmlWriter.WriteString("RentalPriceLength", product.RentalPriceLength, IgnoreExportPoductProperty(p => p.IsRental));
                xmlWriter.WriteString("RentalPricePeriodId", product.RentalPricePeriodId, IgnoreExportPoductProperty(p => p.IsRental));
                xmlWriter.WriteString("IsShipEnabled", product.IsShipEnabled);
                xmlWriter.WriteString("IsFreeShipping", product.IsFreeShipping, IgnoreExportPoductProperty(p => p.FreeShipping));
                xmlWriter.WriteString("ShipSeparately", product.ShipSeparately, IgnoreExportPoductProperty(p => p.ShipSeparately));
                xmlWriter.WriteString("AdditionalShippingCharge", product.AdditionalShippingCharge, IgnoreExportPoductProperty(p => p.AdditionalShippingCharge));
                xmlWriter.WriteString("DeliveryDateId", product.DeliveryDateId, IgnoreExportPoductProperty(p => p.DeliveryDate));
                xmlWriter.WriteString("IsTaxExempt", product.IsTaxExempt);
                xmlWriter.WriteString("TaxCategoryId", product.TaxCategoryId);
                xmlWriter.WriteString("IsTelecommunicationsOrBroadcastingOrElectronicServices", product.IsTelecommunicationsOrBroadcastingOrElectronicServices, IgnoreExportPoductProperty(p => p.TelecommunicationsBroadcastingElectronicServices));
                xmlWriter.WriteString("ManageInventoryMethodId", product.ManageInventoryMethodId);
                xmlWriter.WriteString("UseMultipleWarehouses", product.UseMultipleWarehouses, IgnoreExportPoductProperty(p => p.UseMultipleWarehouses));
                xmlWriter.WriteString("WarehouseId", product.WarehouseId, IgnoreExportPoductProperty(p => p.Warehouse));
                xmlWriter.WriteString("StockQuantity", product.StockQuantity);
                xmlWriter.WriteString("DisplayStockAvailability", product.DisplayStockAvailability, IgnoreExportPoductProperty(p => p.DisplayStockAvailability));
                xmlWriter.WriteString("DisplayStockQuantity", product.DisplayStockQuantity, IgnoreExportPoductProperty(p => p.DisplayStockQuantity));
                xmlWriter.WriteString("MinStockQuantity", product.MinStockQuantity, IgnoreExportPoductProperty(p => p.MinimumStockQuantity));
                xmlWriter.WriteString("LowStockActivityId", product.LowStockActivityId, IgnoreExportPoductProperty(p => p.LowStockActivity));
                xmlWriter.WriteString("NotifyAdminForQuantityBelow", product.NotifyAdminForQuantityBelow, IgnoreExportPoductProperty(p => p.NotifyAdminForQuantityBelow));
                xmlWriter.WriteString("BackorderModeId", product.BackorderModeId, IgnoreExportPoductProperty(p => p.Backorders));
                xmlWriter.WriteString("AllowBackInStockSubscriptions", product.AllowBackInStockSubscriptions, IgnoreExportPoductProperty(p => p.AllowBackInStockSubscriptions));
                xmlWriter.WriteString("OrderMinimumQuantity", product.OrderMinimumQuantity, IgnoreExportPoductProperty(p => p.MinimumCartQuantity));
                xmlWriter.WriteString("OrderMaximumQuantity", product.OrderMaximumQuantity, IgnoreExportPoductProperty(p => p.MaximumCartQuantity));
                xmlWriter.WriteString("AllowedQuantities", product.AllowedQuantities, IgnoreExportPoductProperty(p => p.AllowedQuantities));
                xmlWriter.WriteString("AllowAddingOnlyExistingAttributeCombinations", product.AllowAddingOnlyExistingAttributeCombinations, IgnoreExportPoductProperty(p => p.AllowAddingOnlyExistingAttributeCombinations));
                xmlWriter.WriteString("NotReturnable", product.NotReturnable, IgnoreExportPoductProperty(p => p.NotReturnable));
                xmlWriter.WriteString("DisableBuyButton", product.DisableBuyButton, IgnoreExportPoductProperty(p => p.DisableBuyButton));
                xmlWriter.WriteString("DisableWishlistButton", product.DisableWishlistButton, IgnoreExportPoductProperty(p => p.DisableWishlistButton));
                xmlWriter.WriteString("AvailableForPreOrder", product.AvailableForPreOrder, IgnoreExportPoductProperty(p => p.AvailableForPreOrder));
                xmlWriter.WriteString("PreOrderAvailabilityStartDateTimeUtc", product.PreOrderAvailabilityStartDateTimeUtc, IgnoreExportPoductProperty(p => p.AvailableForPreOrder));
                xmlWriter.WriteString("CallForPrice", product.CallForPrice, IgnoreExportPoductProperty(p => p.CallForPrice));
                xmlWriter.WriteString("Price", product.Price);
                xmlWriter.WriteString("OldPrice", product.OldPrice, IgnoreExportPoductProperty(p => p.OldPrice));
                xmlWriter.WriteString("ProductCost", product.ProductCost, IgnoreExportPoductProperty(p => p.ProductCost));
                xmlWriter.WriteString("SpecialPrice", product.SpecialPrice, IgnoreExportPoductProperty(p => p.SpecialPrice));
                xmlWriter.WriteString("SpecialPriceStartDateTimeUtc", product.SpecialPriceStartDateTimeUtc, IgnoreExportPoductProperty(p => p.SpecialPriceStartDate));
                xmlWriter.WriteString("SpecialPriceEndDateTimeUtc", product.SpecialPriceEndDateTimeUtc, IgnoreExportPoductProperty(p => p.SpecialPriceEndDate));
                xmlWriter.WriteString("CustomerEntersPrice", product.CustomerEntersPrice, IgnoreExportPoductProperty(p => p.CustomerEntersPrice));
                xmlWriter.WriteString("MinimumCustomerEnteredPrice", product.MinimumCustomerEnteredPrice, IgnoreExportPoductProperty(p => p.CustomerEntersPrice));
                xmlWriter.WriteString("MaximumCustomerEnteredPrice", product.MaximumCustomerEnteredPrice, IgnoreExportPoductProperty(p => p.CustomerEntersPrice));
                xmlWriter.WriteString("BasepriceEnabled", product.BasepriceEnabled, IgnoreExportPoductProperty(p => p.PAngV));
                xmlWriter.WriteString("BasepriceAmount", product.BasepriceAmount, IgnoreExportPoductProperty(p => p.PAngV));
                xmlWriter.WriteString("BasepriceUnitId", product.BasepriceUnitId, IgnoreExportPoductProperty(p => p.PAngV));
                xmlWriter.WriteString("BasepriceBaseAmount", product.BasepriceBaseAmount, IgnoreExportPoductProperty(p => p.PAngV));
                xmlWriter.WriteString("BasepriceBaseUnitId", product.BasepriceBaseUnitId, IgnoreExportPoductProperty(p => p.PAngV));
                xmlWriter.WriteString("MarkAsNew", product.MarkAsNew, IgnoreExportPoductProperty(p => p.MarkAsNew));
                xmlWriter.WriteString("MarkAsNewStartDateTimeUtc", product.MarkAsNewStartDateTimeUtc, IgnoreExportPoductProperty(p => p.MarkAsNewStartDate));
                xmlWriter.WriteString("MarkAsNewEndDateTimeUtc", product.MarkAsNewEndDateTimeUtc, IgnoreExportPoductProperty(p => p.MarkAsNewEndDate));
                xmlWriter.WriteString("Weight", product.Weight, IgnoreExportPoductProperty(p => p.Weight));
                xmlWriter.WriteString("Length", product.Length, IgnoreExportPoductProperty(p => p.Dimensions));
                xmlWriter.WriteString("Width", product.Width, IgnoreExportPoductProperty(p => p.Dimensions));
                xmlWriter.WriteString("Height", product.Height, IgnoreExportPoductProperty(p => p.Dimensions));
                xmlWriter.WriteString("Published", product.Published, IgnoreExportPoductProperty(p => p.Published));
                xmlWriter.WriteString("CreatedOnUtc", product.CreatedOnUtc, IgnoreExportPoductProperty(p => p.CreatedOn));
                xmlWriter.WriteString("UpdatedOnUtc", product.UpdatedOnUtc, IgnoreExportPoductProperty(p => p.UpdatedOn));

                if (!IgnoreExportPoductProperty(p => p.Discounts))
                {
                    xmlWriter.WriteStartElement("ProductDiscounts");
                    var discounts = product.AppliedDiscounts;
                    foreach (var discount in discounts)
                    {
                        xmlWriter.WriteStartElement("Discount");
                        xmlWriter.WriteString("DiscountId", discount.Id);
                        xmlWriter.WriteString("Name", discount.Name);
                        xmlWriter.WriteEndElement();
                    }
                    xmlWriter.WriteEndElement();
                }

                if (!IgnoreExportPoductProperty(p => p.TierPrices))
                {
                    xmlWriter.WriteStartElement("TierPrices");
                    var tierPrices = product.TierPrices;
                    foreach (var tierPrice in tierPrices)
                    {
                        xmlWriter.WriteStartElement("TierPrice");
                        xmlWriter.WriteString("TierPriceId", tierPrice.Id);
                        xmlWriter.WriteString("StoreId", tierPrice.StoreId);
                        xmlWriter.WriteString("CustomerRoleId", tierPrice.CustomerRoleId, defaulValue: "0");
                        xmlWriter.WriteString("Quantity", tierPrice.Quantity);
                        xmlWriter.WriteString("Price", tierPrice.Price);
                        xmlWriter.WriteEndElement();
                    }
                    xmlWriter.WriteEndElement();
                }

                if (!IgnoreExportPoductProperty(p => p.ProductAttributes))
                {
                    xmlWriter.WriteStartElement("ProductAttributes");
                    var productAttributMappings =
                        _productAttributeService.GetProductAttributeMappingsByProductId(product.Id);
                    foreach (var productAttributeMapping in productAttributMappings)
                    {
                        xmlWriter.WriteStartElement("ProductAttributeMapping");
                        xmlWriter.WriteString("ProductAttributeMappingId", productAttributeMapping.Id);
                        xmlWriter.WriteString("ProductAttributeId", productAttributeMapping.ProductAttributeId);
                        xmlWriter.WriteString("ProductAttributeName", productAttributeMapping.ProductAttribute.Name);
                        xmlWriter.WriteString("TextPrompt", productAttributeMapping.TextPrompt);
                        xmlWriter.WriteString("IsRequired", productAttributeMapping.IsRequired);
                        xmlWriter.WriteString("AttributeControlTypeId", productAttributeMapping.AttributeControlTypeId);
                        xmlWriter.WriteString("DisplayOrder", productAttributeMapping.DisplayOrder);
                        //validation rules
                        if (productAttributeMapping.ValidationRulesAllowed())
                        {
                            if (productAttributeMapping.ValidationMinLength.HasValue)
                            {
                                xmlWriter.WriteString("ValidationMinLength",
                                    productAttributeMapping.ValidationMinLength.Value);
                            }
                            if (productAttributeMapping.ValidationMaxLength.HasValue)
                            {
                                xmlWriter.WriteString("ValidationMaxLength",
                                    productAttributeMapping.ValidationMaxLength.Value);
                            }
                            if (String.IsNullOrEmpty(productAttributeMapping.ValidationFileAllowedExtensions))
                            {
                                xmlWriter.WriteString("ValidationFileAllowedExtensions",
                                    productAttributeMapping.ValidationFileAllowedExtensions);
                            }
                            if (productAttributeMapping.ValidationFileMaximumSize.HasValue)
                            {
                                xmlWriter.WriteString("ValidationFileMaximumSize",
                                    productAttributeMapping.ValidationFileMaximumSize.Value);
                            }
                            xmlWriter.WriteString("DefaultValue", productAttributeMapping.DefaultValue);
                        }
                        //conditions
                        xmlWriter.WriteElementString("ConditionAttributeXml",
                            productAttributeMapping.ConditionAttributeXml);

                        xmlWriter.WriteStartElement("ProductAttributeValues");
                        var productAttributeValues = productAttributeMapping.ProductAttributeValues;
                        foreach (var productAttributeValue in productAttributeValues)
                        {
                            xmlWriter.WriteStartElement("ProductAttributeValue");
                            xmlWriter.WriteString("ProductAttributeValueId", productAttributeValue.Id);
                            xmlWriter.WriteString("Name", productAttributeValue.Name);
                            xmlWriter.WriteString("AttributeValueTypeId", productAttributeValue.AttributeValueTypeId);
                            xmlWriter.WriteString("AssociatedProductId", productAttributeValue.AssociatedProductId);
                            xmlWriter.WriteString("ColorSquaresRgb", productAttributeValue.ColorSquaresRgb);
                            xmlWriter.WriteString("ImageSquaresPictureId", productAttributeValue.ImageSquaresPictureId);
                            xmlWriter.WriteString("PriceAdjustment", productAttributeValue.PriceAdjustment);
                            xmlWriter.WriteString("WeightAdjustment", productAttributeValue.WeightAdjustment);
                            xmlWriter.WriteString("Cost", productAttributeValue.Cost);
                            xmlWriter.WriteString("CustomerEntersQty", productAttributeValue.CustomerEntersQty);
                            xmlWriter.WriteString("Quantity", productAttributeValue.Quantity);
                            xmlWriter.WriteString("IsPreSelected", productAttributeValue.IsPreSelected);
                            xmlWriter.WriteString("DisplayOrder", productAttributeValue.DisplayOrder);
                            xmlWriter.WriteString("PictureId", productAttributeValue.PictureId);
                            xmlWriter.WriteEndElement();
                        }
                        xmlWriter.WriteEndElement();

                        xmlWriter.WriteEndElement();
                    }
                    xmlWriter.WriteEndElement();
                }
                xmlWriter.WriteStartElement("ProductPictures");
                var productPictures = product.ProductPictures;
                foreach (var productPicture in productPictures)
                {
                    xmlWriter.WriteStartElement("ProductPicture");
                    xmlWriter.WriteString("ProductPictureId", productPicture.Id);
                    xmlWriter.WriteString("PictureId", productPicture.PictureId);
                    xmlWriter.WriteString("DisplayOrder", productPicture.DisplayOrder);
                    xmlWriter.WriteEndElement();
                }
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement("ProductCategories");
                var productCategories = _categoryService.GetProductCategoriesByProductId(product.Id);
                if (productCategories != null)
                {
                    foreach (var productCategory in productCategories)
                    {
                        xmlWriter.WriteStartElement("ProductCategory");
                        xmlWriter.WriteString("ProductCategoryId", productCategory.Id);
                        xmlWriter.WriteString("CategoryId", productCategory.CategoryId);
                        xmlWriter.WriteString("IsFeaturedProduct", productCategory.IsFeaturedProduct);
                        xmlWriter.WriteString("DisplayOrder", productCategory.DisplayOrder);
                        xmlWriter.WriteEndElement();
                    }
                }
                xmlWriter.WriteEndElement();

                if (!IgnoreExportPoductProperty(p => p.Manufacturers))
                {
                    xmlWriter.WriteStartElement("ProductManufacturers");
                    var productManufacturers = _manufacturerService.GetProductManufacturersByProductId(product.Id);
                    if (productManufacturers != null)
                    {
                        foreach (var productManufacturer in productManufacturers)
                        {
                            xmlWriter.WriteStartElement("ProductManufacturer");
                            xmlWriter.WriteString("ProductManufacturerId", productManufacturer.Id);
                            xmlWriter.WriteString("ManufacturerId", productManufacturer.ManufacturerId);
                            xmlWriter.WriteString("IsFeaturedProduct", productManufacturer.IsFeaturedProduct);
                            xmlWriter.WriteString("DisplayOrder", productManufacturer.DisplayOrder);
                            xmlWriter.WriteEndElement();
                        }
                    }
                    xmlWriter.WriteEndElement();
                }

                if (!IgnoreExportPoductProperty(p => p.SpecificationAttributes))
                {
                    xmlWriter.WriteStartElement("ProductSpecificationAttributes");
                    var productSpecificationAttributes = product.ProductSpecificationAttributes;
                    foreach (var productSpecificationAttribute in productSpecificationAttributes)
                    {
                        xmlWriter.WriteStartElement("ProductSpecificationAttribute");
                        xmlWriter.WriteString("ProductSpecificationAttributeId", productSpecificationAttribute.Id);
                        xmlWriter.WriteString("SpecificationAttributeOptionId", productSpecificationAttribute.SpecificationAttributeOptionId);
                        xmlWriter.WriteString("CustomValue", productSpecificationAttribute.CustomValue);
                        xmlWriter.WriteString("AllowFiltering", productSpecificationAttribute.AllowFiltering);
                        xmlWriter.WriteString("ShowOnProductPage", productSpecificationAttribute.ShowOnProductPage);
                        xmlWriter.WriteString("DisplayOrder", productSpecificationAttribute.DisplayOrder);
                        xmlWriter.WriteEndElement();
                    }
                    xmlWriter.WriteEndElement();
                }

                if (!IgnoreExportPoductProperty(p => p.ProductTags))
                {
                    xmlWriter.WriteStartElement("ProductTags");
                    var productTags = product.ProductTags;
                    foreach (var productTag in productTags)
                    {
                        xmlWriter.WriteStartElement("ProductTag");
                        xmlWriter.WriteString("Id", productTag.Id);
                        xmlWriter.WriteString("Name", productTag.Name);
                        xmlWriter.WriteEndElement();
                    }
                    xmlWriter.WriteEndElement();
                }

                xmlWriter.WriteEndElement();
            }

            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
            xmlWriter.Close();
            return stringWriter.ToString();
        }

        /// <summary>
        /// Export products to XLSX
        /// </summary>
        /// <param name="products">Products</param>
        public virtual byte[] ExportProductsToXlsx(IEnumerable<Product> products)
        {
            var properties = new[]
            {
                new PropertyByName<Product>("ProductType", p => p.ProductTypeId, IgnoreExportPoductProperty(p => p.ProductType))
                {
                    DropDownElements = ProductType.SimpleProduct.ToSelectList(useLocalization: false)
                },
                new PropertyByName<Product>("ParentGroupedProductId", p => p.ParentGroupedProductId, IgnoreExportPoductProperty(p => p.ProductType)),
                new PropertyByName<Product>("VisibleIndividually", p => p.VisibleIndividually, IgnoreExportPoductProperty(p => p.VisibleIndividually)),
                new PropertyByName<Product>("Name", p => p.Name),
                new PropertyByName<Product>("ShortDescription", p => p.ShortDescription),
                new PropertyByName<Product>("FullDescription", p => p.FullDescription),
                new PropertyByName<Product>("Vendor", p => p.VendorId, IgnoreExportPoductProperty(p => p.Vendor))
                {
                    DropDownElements = _vendorService.GetAllVendors(showHidden:true).Select(v => v as BaseEntity).ToSelectList(p => (p as Vendor).Return(v => v.Name, String.Empty)),
                    AllowBlank = true
                },
                new PropertyByName<Product>("ProductTemplate", p => p.ProductTemplateId, IgnoreExportPoductProperty(p => p.ProductTemplate))
                {
                    DropDownElements = _productTemplateService.GetAllProductTemplates().Select(pt => pt as BaseEntity).ToSelectList(p => (p as ProductTemplate).Return(pt => pt.Name, String.Empty)),
                },
                new PropertyByName<Product>("ShowOnHomePage", p => p.ShowOnHomePage, IgnoreExportPoductProperty(p => p.ShowOnHomePage)),
                new PropertyByName<Product>("MetaKeywords", p => p.MetaKeywords, IgnoreExportPoductProperty(p => p.Seo)),
                new PropertyByName<Product>("MetaDescription", p => p.MetaDescription, IgnoreExportPoductProperty(p => p.Seo)),
                new PropertyByName<Product>("MetaTitle", p => p.MetaTitle, IgnoreExportPoductProperty(p => p.Seo)),
                new PropertyByName<Product>("SeName", p => p.GetSeName(0), IgnoreExportPoductProperty(p => p.Seo)),
                new PropertyByName<Product>("AllowCustomerReviews", p => p.AllowCustomerReviews, IgnoreExportPoductProperty(p => p.AllowCustomerReviews)),
                new PropertyByName<Product>("Published", p => p.Published, IgnoreExportPoductProperty(p => p.Published)),
                new PropertyByName<Product>("SKU", p => p.Sku),
                new PropertyByName<Product>("ManufacturerPartNumber", p => p.ManufacturerPartNumber, IgnoreExportPoductProperty(p => p.ManufacturerPartNumber)),
                new PropertyByName<Product>("Gtin", p => p.Gtin, IgnoreExportPoductProperty(p => p.GTIN)),
                new PropertyByName<Product>("IsGiftCard", p => p.IsGiftCard, IgnoreExportPoductProperty(p => p.IsGiftCard)),
                new PropertyByName<Product>("GiftCardType", p => p.GiftCardTypeId, IgnoreExportPoductProperty(p => p.IsGiftCard))
                {
                    DropDownElements = GiftCardType.Virtual.ToSelectList(useLocalization: false)
                },
                new PropertyByName<Product>("OverriddenGiftCardAmount", p => p.OverriddenGiftCardAmount, IgnoreExportPoductProperty(p => p.IsGiftCard)),
                new PropertyByName<Product>("RequireOtherProducts", p => p.RequireOtherProducts, IgnoreExportPoductProperty(p => p.RequireOtherProductsAddedToTheCart)),
                new PropertyByName<Product>("RequiredProductIds", p => p.RequiredProductIds, IgnoreExportPoductProperty(p => p.RequireOtherProductsAddedToTheCart)),
                new PropertyByName<Product>("AutomaticallyAddRequiredProducts", p => p.AutomaticallyAddRequiredProducts, IgnoreExportPoductProperty(p => p.RequireOtherProductsAddedToTheCart)),
                new PropertyByName<Product>("IsDownload", p => p.IsDownload, IgnoreExportPoductProperty(p => p.DownloadableProduct)),
                new PropertyByName<Product>("DownloadId", p => p.DownloadId, IgnoreExportPoductProperty(p => p.DownloadableProduct)),
                new PropertyByName<Product>("UnlimitedDownloads", p => p.UnlimitedDownloads, IgnoreExportPoductProperty(p => p.DownloadableProduct)),
                new PropertyByName<Product>("MaxNumberOfDownloads", p => p.MaxNumberOfDownloads, IgnoreExportPoductProperty(p => p.DownloadableProduct)),
                new PropertyByName<Product>("DownloadActivationType", p => p.DownloadActivationTypeId, IgnoreExportPoductProperty(p => p.DownloadableProduct))
                {
                    DropDownElements = DownloadActivationType.Manually.ToSelectList(useLocalization: false)
                },
                new PropertyByName<Product>("HasSampleDownload", p => p.HasSampleDownload, IgnoreExportPoductProperty(p => p.DownloadableProduct)),
                new PropertyByName<Product>("SampleDownloadId", p => p.SampleDownloadId, IgnoreExportPoductProperty(p => p.DownloadableProduct)),
                new PropertyByName<Product>("HasUserAgreement", p => p.HasUserAgreement, IgnoreExportPoductProperty(p => p.DownloadableProduct)),
                new PropertyByName<Product>("UserAgreementText", p => p.UserAgreementText, IgnoreExportPoductProperty(p => p.DownloadableProduct)),
                new PropertyByName<Product>("IsRecurring", p => p.IsRecurring, IgnoreExportPoductProperty(p => p.RecurringProduct)),
                new PropertyByName<Product>("RecurringCycleLength", p => p.RecurringCycleLength, IgnoreExportPoductProperty(p => p.RecurringProduct)),
                new PropertyByName<Product>("RecurringCyclePeriod", p => p.RecurringCyclePeriodId, IgnoreExportPoductProperty(p => p.RecurringProduct))
                {
                    DropDownElements = RecurringProductCyclePeriod.Days.ToSelectList(useLocalization: false),
                    AllowBlank = true
                },
                new PropertyByName<Product>("RecurringTotalCycles", p => p.RecurringTotalCycles, IgnoreExportPoductProperty(p => p.RecurringProduct)),
                new PropertyByName<Product>("IsRental", p => p.IsRental, IgnoreExportPoductProperty(p => p.IsRental)),
                new PropertyByName<Product>("RentalPriceLength", p => p.RentalPriceLength, IgnoreExportPoductProperty(p => p.IsRental)),
                new PropertyByName<Product>("RentalPricePeriod", p => p.RentalPricePeriodId, IgnoreExportPoductProperty(p => p.IsRental))
                {
                    DropDownElements = RentalPricePeriod.Days.ToSelectList(useLocalization: false),
                    AllowBlank = true
                },
                new PropertyByName<Product>("IsShipEnabled", p => p.IsShipEnabled),
                new PropertyByName<Product>("IsFreeShipping", p => p.IsFreeShipping, IgnoreExportPoductProperty(p => p.FreeShipping)),
                new PropertyByName<Product>("ShipSeparately", p => p.ShipSeparately, IgnoreExportPoductProperty(p => p.ShipSeparately)),
                new PropertyByName<Product>("AdditionalShippingCharge", p => p.AdditionalShippingCharge, IgnoreExportPoductProperty(p => p.AdditionalShippingCharge)),
                new PropertyByName<Product>("DeliveryDate", p => p.DeliveryDateId, IgnoreExportPoductProperty(p => p.DeliveryDate))
                {
                    DropDownElements = _shippingService.GetAllDeliveryDates().Select(dd => dd as BaseEntity).ToSelectList(p => (p as DeliveryDate).Return(dd => dd.Name, String.Empty)),
                    AllowBlank = true
                },
                new PropertyByName<Product>("IsTaxExempt", p => p.IsTaxExempt),
                new PropertyByName<Product>("TaxCategory", p => p.TaxCategoryId)
                {
                    DropDownElements = _taxCategoryService.GetAllTaxCategories().Select(tc => tc as BaseEntity).ToSelectList(p => (p as TaxCategory).Return(tc => tc.Name, String.Empty)),
                    AllowBlank = true
                },
                new PropertyByName<Product>("IsTelecommunicationsOrBroadcastingOrElectronicServices", p => p.IsTelecommunicationsOrBroadcastingOrElectronicServices, IgnoreExportPoductProperty(p => p.TelecommunicationsBroadcastingElectronicServices)),
                new PropertyByName<Product>("ManageInventoryMethod", p => p.ManageInventoryMethodId)
                {
                    DropDownElements = ManageInventoryMethod.DontManageStock.ToSelectList(useLocalization: false)
                },
                new PropertyByName<Product>("UseMultipleWarehouses", p => p.UseMultipleWarehouses, IgnoreExportPoductProperty(p => p.UseMultipleWarehouses)),
                new PropertyByName<Product>("WarehouseId", p => p.WarehouseId, IgnoreExportPoductProperty(p => p.Warehouse)),
                new PropertyByName<Product>("StockQuantity", p => p.StockQuantity),
                new PropertyByName<Product>("DisplayStockAvailability", p => p.DisplayStockAvailability, IgnoreExportPoductProperty(p => p.DisplayStockAvailability)),
                new PropertyByName<Product>("DisplayStockQuantity", p => p.DisplayStockQuantity, IgnoreExportPoductProperty(p => p.DisplayStockQuantity)),
                new PropertyByName<Product>("MinStockQuantity", p => p.MinStockQuantity, IgnoreExportPoductProperty(p => p.MinimumStockQuantity)),
                new PropertyByName<Product>("LowStockActivity", p => p.LowStockActivityId, IgnoreExportPoductProperty(p => p.LowStockActivity))
                {
                    DropDownElements = LowStockActivity.Nothing.ToSelectList(useLocalization: false)
                },
                new PropertyByName<Product>("NotifyAdminForQuantityBelow", p => p.NotifyAdminForQuantityBelow, IgnoreExportPoductProperty(p => p.NotifyAdminForQuantityBelow)),
                new PropertyByName<Product>("BackorderMode", p => p.BackorderModeId, IgnoreExportPoductProperty(p => p.Backorders))
                {
                    DropDownElements = BackorderMode.NoBackorders.ToSelectList(useLocalization: false)
                },
                new PropertyByName<Product>("AllowBackInStockSubscriptions", p => p.AllowBackInStockSubscriptions, IgnoreExportPoductProperty(p => p.AllowBackInStockSubscriptions)),
                new PropertyByName<Product>("OrderMinimumQuantity", p => p.OrderMinimumQuantity, IgnoreExportPoductProperty(p => p.MinimumCartQuantity)),
                new PropertyByName<Product>("OrderMaximumQuantity", p => p.OrderMaximumQuantity, IgnoreExportPoductProperty(p => p.MaximumCartQuantity)),
                new PropertyByName<Product>("AllowedQuantities", p => p.AllowedQuantities, IgnoreExportPoductProperty(p => p.AllowedQuantities)),
                new PropertyByName<Product>("AllowAddingOnlyExistingAttributeCombinations", p => p.AllowAddingOnlyExistingAttributeCombinations, IgnoreExportPoductProperty(p => p.AllowAddingOnlyExistingAttributeCombinations)),
                new PropertyByName<Product>("NotReturnable", p => p.NotReturnable, IgnoreExportPoductProperty(p => p.NotReturnable)),
                new PropertyByName<Product>("DisableBuyButton", p => p.DisableBuyButton, IgnoreExportPoductProperty(p => p.DisableBuyButton)),
                new PropertyByName<Product>("DisableWishlistButton", p => p.DisableWishlistButton, IgnoreExportPoductProperty(p => p.DisableWishlistButton)),
                new PropertyByName<Product>("AvailableForPreOrder", p => p.AvailableForPreOrder, IgnoreExportPoductProperty(p => p.AvailableForPreOrder)),
                new PropertyByName<Product>("PreOrderAvailabilityStartDateTimeUtc", p => p.PreOrderAvailabilityStartDateTimeUtc, IgnoreExportPoductProperty(p => p.AvailableForPreOrder)),
                new PropertyByName<Product>("CallForPrice", p => p.CallForPrice, IgnoreExportPoductProperty(p => p.CallForPrice)),
                new PropertyByName<Product>("Price", p => p.Price),
                new PropertyByName<Product>("OldPrice", p => p.OldPrice, IgnoreExportPoductProperty(p => p.OldPrice)),
                new PropertyByName<Product>("ProductCost", p => p.ProductCost, IgnoreExportPoductProperty(p => p.ProductCost)),
                new PropertyByName<Product>("SpecialPrice", p => p.SpecialPrice, IgnoreExportPoductProperty(p => p.SpecialPrice)),
                new PropertyByName<Product>("SpecialPriceStartDateTimeUtc", p => p.SpecialPriceStartDateTimeUtc, IgnoreExportPoductProperty(p => p.SpecialPriceStartDate)),
                new PropertyByName<Product>("SpecialPriceEndDateTimeUtc", p => p.SpecialPriceEndDateTimeUtc, IgnoreExportPoductProperty(p => p.SpecialPriceEndDate)),
                new PropertyByName<Product>("CustomerEntersPrice", p => p.CustomerEntersPrice, IgnoreExportPoductProperty(p => p.CustomerEntersPrice)),
                new PropertyByName<Product>("MinimumCustomerEnteredPrice", p => p.MinimumCustomerEnteredPrice, IgnoreExportPoductProperty(p => p.CustomerEntersPrice)),
                new PropertyByName<Product>("MaximumCustomerEnteredPrice", p => p.MaximumCustomerEnteredPrice, IgnoreExportPoductProperty(p => p.CustomerEntersPrice)),
                new PropertyByName<Product>("BasepriceEnabled", p => p.BasepriceEnabled, IgnoreExportPoductProperty(p => p.PAngV)),
                new PropertyByName<Product>("BasepriceAmount", p => p.BasepriceAmount, IgnoreExportPoductProperty(p => p.PAngV)),
                new PropertyByName<Product>("BasepriceUnit", p => p.BasepriceUnitId, IgnoreExportPoductProperty(p => p.PAngV))
                {
                    DropDownElements = _measureService.GetAllMeasureWeights().Select(mw => mw as BaseEntity).ToSelectList(p => (p as MeasureWeight).Return(mw => mw.Name, String.Empty)),
                    AllowBlank = true
                },
                new PropertyByName<Product>("BasepriceBaseAmount", p => p.BasepriceBaseAmount, IgnoreExportPoductProperty(p => p.PAngV)),
                new PropertyByName<Product>("BasepriceBaseUnit", p => p.BasepriceBaseUnitId, IgnoreExportPoductProperty(p => p.PAngV))
                {
                    DropDownElements = _measureService.GetAllMeasureWeights().Select(mw => mw as BaseEntity).ToSelectList(p => (p as MeasureWeight).Return(mw => mw.Name, String.Empty)),
                    AllowBlank = true
                },
                new PropertyByName<Product>("MarkAsNew", p => p.MarkAsNew, IgnoreExportPoductProperty(p => p.MarkAsNew)),
                new PropertyByName<Product>("MarkAsNewStartDateTimeUtc", p => p.MarkAsNewStartDateTimeUtc, IgnoreExportPoductProperty(p => p.MarkAsNewStartDate)),
                new PropertyByName<Product>("MarkAsNewEndDateTimeUtc", p => p.MarkAsNewEndDateTimeUtc, IgnoreExportPoductProperty(p => p.MarkAsNewEndDate)),
                new PropertyByName<Product>("Weight", p => p.Weight, IgnoreExportPoductProperty(p => p.Weight)),
                new PropertyByName<Product>("Length", p => p.Length, IgnoreExportPoductProperty(p => p.Dimensions)),
                new PropertyByName<Product>("Width", p => p.Width, IgnoreExportPoductProperty(p => p.Dimensions)),
                new PropertyByName<Product>("Height", p => p.Height, IgnoreExportPoductProperty(p => p.Dimensions)),
                new PropertyByName<Product>("Categories", GetCategories),
                new PropertyByName<Product>("Manufacturers", GetManufacturers, IgnoreExportPoductProperty(p => p.Manufacturers)),
                new PropertyByName<Product>("ProductTags", GetProductTags, IgnoreExportPoductProperty(p => p.ProductTags)),
                new PropertyByName<Product>("Picture1", p => GetPictures(p)[0]),
                new PropertyByName<Product>("Picture2", p => GetPictures(p)[1]),
                new PropertyByName<Product>("Picture3", p => GetPictures(p)[2])
            };

            var productList = products.ToList();
            var productAdvancedMode = _workContext.CurrentCustomer.GetAttribute<bool>("product-advanced-mode");

            if (_catalogSettings.ExportImportProductAttributes)
            {
                if (productAdvancedMode || _productEditorSettings.ProductAttributes)
                    return ExportProductsToXlsxWithAttributes(properties, productList);
            }

            return ExportToXlsx(properties, productList);
        }

        /// <summary>
        /// Export order list to xml
        /// </summary>
        /// <param name="orders">Orders</param>
        /// <returns>Result in XML format</returns>
        public virtual string ExportOrdersToXml(IList<Order> orders)
        {
            var sb = new StringBuilder();
            var stringWriter = new StringWriter(sb);
            var xmlWriter = new XmlTextWriter(stringWriter);
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("Orders");
            xmlWriter.WriteAttributeString("Version", NopVersion.CurrentVersion);

            foreach (var order in orders)
            {
                xmlWriter.WriteStartElement("Order");

                xmlWriter.WriteElementString("OrderId", null, order.Id.ToString());
                xmlWriter.WriteElementString("OrderGuid", null, order.OrderGuid.ToString());
                xmlWriter.WriteElementString("StoreId", null, order.StoreId.ToString());
                xmlWriter.WriteElementString("CustomerId", null, order.CustomerId.ToString());
                xmlWriter.WriteElementString("OrderStatusId", null, order.OrderStatusId.ToString());
                xmlWriter.WriteElementString("PaymentStatusId", null, order.PaymentStatusId.ToString());
                xmlWriter.WriteElementString("ShippingStatusId", null, order.ShippingStatusId.ToString());
                xmlWriter.WriteElementString("CustomerLanguageId", null, order.CustomerLanguageId.ToString());
                xmlWriter.WriteElementString("CustomerTaxDisplayTypeId", null, order.CustomerTaxDisplayTypeId.ToString());
                xmlWriter.WriteElementString("CustomerIp", null, order.CustomerIp);
                xmlWriter.WriteElementString("OrderSubtotalInclTax", null, order.OrderSubtotalInclTax.ToString());
                xmlWriter.WriteElementString("OrderSubtotalExclTax", null, order.OrderSubtotalExclTax.ToString());
                xmlWriter.WriteElementString("OrderSubTotalDiscountInclTax", null, order.OrderSubTotalDiscountInclTax.ToString());
                xmlWriter.WriteElementString("OrderSubTotalDiscountExclTax", null, order.OrderSubTotalDiscountExclTax.ToString());
                xmlWriter.WriteElementString("OrderShippingInclTax", null, order.OrderShippingInclTax.ToString());
                xmlWriter.WriteElementString("OrderShippingExclTax", null, order.OrderShippingExclTax.ToString());
                xmlWriter.WriteElementString("PaymentMethodAdditionalFeeInclTax", null, order.PaymentMethodAdditionalFeeInclTax.ToString());
                xmlWriter.WriteElementString("PaymentMethodAdditionalFeeExclTax", null, order.PaymentMethodAdditionalFeeExclTax.ToString());
                xmlWriter.WriteElementString("TaxRates", null, order.TaxRates);
                xmlWriter.WriteElementString("OrderTax", null, order.OrderTax.ToString());
                xmlWriter.WriteElementString("OrderTotal", null, order.OrderTotal.ToString());
                xmlWriter.WriteElementString("RefundedAmount", null, order.RefundedAmount.ToString());
                xmlWriter.WriteElementString("OrderDiscount", null, order.OrderDiscount.ToString());
                xmlWriter.WriteElementString("CurrencyRate", null, order.CurrencyRate.ToString());
                xmlWriter.WriteElementString("CustomerCurrencyCode", null, order.CustomerCurrencyCode);
                xmlWriter.WriteElementString("AffiliateId", null, order.AffiliateId.ToString());
                xmlWriter.WriteElementString("AllowStoringCreditCardNumber", null, order.AllowStoringCreditCardNumber.ToString());
                xmlWriter.WriteElementString("CardType", null, order.CardType);
                xmlWriter.WriteElementString("CardName", null, order.CardName);
                xmlWriter.WriteElementString("CardNumber", null, order.CardNumber);
                xmlWriter.WriteElementString("MaskedCreditCardNumber", null, order.MaskedCreditCardNumber);
                xmlWriter.WriteElementString("CardCvv2", null, order.CardCvv2);
                xmlWriter.WriteElementString("CardExpirationMonth", null, order.CardExpirationMonth);
                xmlWriter.WriteElementString("CardExpirationYear", null, order.CardExpirationYear);
                xmlWriter.WriteElementString("PaymentMethodSystemName", null, order.PaymentMethodSystemName);
                xmlWriter.WriteElementString("AuthorizationTransactionId", null, order.AuthorizationTransactionId);
                xmlWriter.WriteElementString("AuthorizationTransactionCode", null, order.AuthorizationTransactionCode);
                xmlWriter.WriteElementString("AuthorizationTransactionResult", null, order.AuthorizationTransactionResult);
                xmlWriter.WriteElementString("CaptureTransactionId", null, order.CaptureTransactionId);
                xmlWriter.WriteElementString("CaptureTransactionResult", null, order.CaptureTransactionResult);
                xmlWriter.WriteElementString("SubscriptionTransactionId", null, order.SubscriptionTransactionId);
                xmlWriter.WriteElementString("PaidDateUtc", null, (order.PaidDateUtc == null) ? string.Empty : order.PaidDateUtc.Value.ToString());
                xmlWriter.WriteElementString("ShippingMethod", null, order.ShippingMethod);
                xmlWriter.WriteElementString("ShippingRateComputationMethodSystemName", null, order.ShippingRateComputationMethodSystemName);
                xmlWriter.WriteElementString("CustomValuesXml", null, order.CustomValuesXml);
                xmlWriter.WriteElementString("VatNumber", null, order.VatNumber);
                xmlWriter.WriteElementString("Deleted", null, order.Deleted.ToString());
                xmlWriter.WriteElementString("CreatedOnUtc", null, order.CreatedOnUtc.ToString());

                //products
                var orderItems = order.OrderItems;
                if (orderItems.Any())
                {
                    xmlWriter.WriteStartElement("OrderItems");
                    foreach (var orderItem in orderItems)
                    {
                        xmlWriter.WriteStartElement("OrderItem");
                        xmlWriter.WriteElementString("Id", null, orderItem.Id.ToString());
                        xmlWriter.WriteElementString("OrderItemGuid", null, orderItem.OrderItemGuid.ToString());
                        xmlWriter.WriteElementString("ProductId", null, orderItem.ProductId.ToString());

                        var product = orderItem.Product;
                        xmlWriter.WriteElementString("ProductName", null, product.Name);
                        xmlWriter.WriteElementString("UnitPriceInclTax", null, orderItem.UnitPriceInclTax.ToString());
                        xmlWriter.WriteElementString("UnitPriceExclTax", null, orderItem.UnitPriceExclTax.ToString());
                        xmlWriter.WriteElementString("PriceInclTax", null, orderItem.PriceInclTax.ToString());
                        xmlWriter.WriteElementString("PriceExclTax", null, orderItem.PriceExclTax.ToString());
                        xmlWriter.WriteElementString("DiscountAmountInclTax", null, orderItem.DiscountAmountInclTax.ToString());
                        xmlWriter.WriteElementString("DiscountAmountExclTax", null, orderItem.DiscountAmountExclTax.ToString());
                        xmlWriter.WriteElementString("OriginalProductCost", null, orderItem.OriginalProductCost.ToString());
                        xmlWriter.WriteElementString("AttributeDescription", null, orderItem.AttributeDescription);
                        xmlWriter.WriteElementString("AttributesXml", null, orderItem.AttributesXml);
                        xmlWriter.WriteElementString("Quantity", null, orderItem.Quantity.ToString());
                        xmlWriter.WriteElementString("DownloadCount", null, orderItem.DownloadCount.ToString());
                        xmlWriter.WriteElementString("IsDownloadActivated", null, orderItem.IsDownloadActivated.ToString());
                        xmlWriter.WriteElementString("LicenseDownloadId", null, orderItem.LicenseDownloadId.ToString());
                        var rentalStartDate = orderItem.RentalStartDateUtc.HasValue ? orderItem.Product.FormatRentalDate(orderItem.RentalStartDateUtc.Value) : String.Empty;
                        xmlWriter.WriteElementString("RentalStartDateUtc", null, rentalStartDate);
                        var rentalEndDate = orderItem.RentalEndDateUtc.HasValue ? orderItem.Product.FormatRentalDate(orderItem.RentalEndDateUtc.Value) : String.Empty;
                        xmlWriter.WriteElementString("RentalEndDateUtc", null, rentalEndDate);
                        xmlWriter.WriteEndElement();
                    }
                    xmlWriter.WriteEndElement();
                }

                //shipments
                var shipments = order.Shipments.OrderBy(x => x.CreatedOnUtc).ToList();
                if (shipments.Any())
                {
                    xmlWriter.WriteStartElement("Shipments");
                    foreach (var shipment in shipments)
                    {
                        xmlWriter.WriteStartElement("Shipment");
                        xmlWriter.WriteElementString("ShipmentId", null, shipment.Id.ToString());
                        xmlWriter.WriteElementString("TrackingNumber", null, shipment.TrackingNumber);
                        xmlWriter.WriteElementString("TotalWeight", null, shipment.TotalWeight.HasValue ? shipment.TotalWeight.Value.ToString() : String.Empty);

                        xmlWriter.WriteElementString("ShippedDateUtc", null, shipment.ShippedDateUtc.HasValue ? 
                            shipment.ShippedDateUtc.ToString() : String.Empty);
                        xmlWriter.WriteElementString("DeliveryDateUtc", null, shipment.DeliveryDateUtc.HasValue ? 
                            shipment.DeliveryDateUtc.Value.ToString() : String.Empty);
                        xmlWriter.WriteElementString("CreatedOnUtc", null, shipment.CreatedOnUtc.ToString());
                        xmlWriter.WriteEndElement();
                    }
                    xmlWriter.WriteEndElement();
                }
                xmlWriter.WriteEndElement();
            }

            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
            xmlWriter.Close();
            return stringWriter.ToString();
        }

        /// <summary>
        /// Export orders to XLSX
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="orders">Orders</param>
        public virtual byte[] ExportOrdersToXlsx(IList<Order> orders)
        {
            //property array
            var properties = new[]
            {
                    new PropertyByName<Order>("OrderId", p => p.Id),
                    new PropertyByName<Order>("StoreId", p => p.StoreId),
                    new PropertyByName<Order>("OrderGuid", p => p.OrderGuid),
                    new PropertyByName<Order>("CustomerId", p => p.CustomerId),
                    new PropertyByName<Order>("OrderStatusId", p => p.OrderStatusId),
                    new PropertyByName<Order>("PaymentStatusId", p => p.PaymentStatusId),
                    new PropertyByName<Order>("ShippingStatusId", p => p.ShippingStatusId),
                    new PropertyByName<Order>("OrderSubtotalInclTax", p => p.OrderSubtotalInclTax),
                    new PropertyByName<Order>("OrderSubtotalExclTax", p => p.OrderSubtotalExclTax),
                    new PropertyByName<Order>("OrderSubTotalDiscountInclTax", p => p.OrderSubTotalDiscountInclTax),
                    new PropertyByName<Order>("OrderSubTotalDiscountExclTax", p => p.OrderSubTotalDiscountExclTax),
                    new PropertyByName<Order>("OrderShippingInclTax", p => p.OrderShippingInclTax),
                    new PropertyByName<Order>("OrderShippingExclTax", p => p.OrderShippingExclTax),
                    new PropertyByName<Order>("PaymentMethodAdditionalFeeInclTax", p => p.PaymentMethodAdditionalFeeInclTax),
                    new PropertyByName<Order>("PaymentMethodAdditionalFeeExclTax", p => p.PaymentMethodAdditionalFeeExclTax),
                    new PropertyByName<Order>("TaxRates", p => p.TaxRates),
                    new PropertyByName<Order>("OrderTax", p => p.OrderTax),
                    new PropertyByName<Order>("OrderTotal", p => p.OrderTotal),
                    new PropertyByName<Order>("RefundedAmount", p => p.RefundedAmount),
                    new PropertyByName<Order>("OrderDiscount", p => p.OrderDiscount),
                    new PropertyByName<Order>("CurrencyRate", p => p.CurrencyRate),
                    new PropertyByName<Order>("CustomerCurrencyCode", p => p.CustomerCurrencyCode),
                    new PropertyByName<Order>("AffiliateId", p => p.AffiliateId),
                    new PropertyByName<Order>("PaymentMethodSystemName", p => p.PaymentMethodSystemName),
                    new PropertyByName<Order>("ShippingPickUpInStore", p => p.PickUpInStore),
                    new PropertyByName<Order>("ShippingMethod", p => p.ShippingMethod),
                    new PropertyByName<Order>("ShippingRateComputationMethodSystemName", p => p.ShippingRateComputationMethodSystemName),
                    new PropertyByName<Order>("CustomValuesXml", p => p.CustomValuesXml),
                    new PropertyByName<Order>("VatNumber", p => p.VatNumber),
                    new PropertyByName<Order>("CreatedOnUtc", p => p.CreatedOnUtc.ToOADate()),
                    new PropertyByName<Order>("BillingFirstName", p => p.BillingAddress.Return(billingAddress => billingAddress.FirstName, String.Empty)),
                    new PropertyByName<Order>("BillingLastName", p => p.BillingAddress.Return(billingAddress => billingAddress.LastName, String.Empty)),
                    new PropertyByName<Order>("BillingEmail", p => p.BillingAddress.Return(billingAddress => billingAddress.Email, String.Empty)),
                    new PropertyByName<Order>("BillingCompany", p => p.BillingAddress.Return(billingAddress => billingAddress.Company, String.Empty)),
                    new PropertyByName<Order>("BillingCountry", p => p.BillingAddress.Return(billingAddress => billingAddress.Country, null).Return(country => country.Name, String.Empty)),
                    new PropertyByName<Order>("BillingStateProvince", p => p.BillingAddress.Return(billingAddress => billingAddress.StateProvince, null).Return(stateProvince => stateProvince.Name, String.Empty)),
                    new PropertyByName<Order>("BillingCity", p => p.BillingAddress.Return(billingAddress => billingAddress.City, String.Empty)),
                    new PropertyByName<Order>("BillingAddress1", p => p.BillingAddress.Return(billingAddress => billingAddress.Address1, String.Empty)),
                    new PropertyByName<Order>("BillingAddress2", p => p.BillingAddress.Return(billingAddress => billingAddress.Address2, String.Empty)),
                    new PropertyByName<Order>("BillingZipPostalCode", p => p.BillingAddress.Return(billingAddress => billingAddress.ZipPostalCode, String.Empty)),
                    new PropertyByName<Order>("BillingPhoneNumber", p => p.BillingAddress.Return(billingAddress => billingAddress.PhoneNumber, String.Empty)),
                    new PropertyByName<Order>("BillingFaxNumber", p => p.BillingAddress.Return(billingAddress => billingAddress.FaxNumber, String.Empty)),
                    new PropertyByName<Order>("ShippingFirstName", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.FirstName, String.Empty)),
                    new PropertyByName<Order>("ShippingLastName", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.LastName, String.Empty)),
                    new PropertyByName<Order>("ShippingEmail", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.Email, String.Empty)),
                    new PropertyByName<Order>("ShippingCompany", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.Company, String.Empty)),
                    new PropertyByName<Order>("ShippingCountry", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.Country, null).Return(country => country.Name, String.Empty)),
                    new PropertyByName<Order>("ShippingStateProvince", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.StateProvince, null).Return(stateProvince => stateProvince.Name, String.Empty)),
                    new PropertyByName<Order>("ShippingCity", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.City, String.Empty)),
                    new PropertyByName<Order>("ShippingAddress1", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.Address1, String.Empty)),
                    new PropertyByName<Order>("ShippingAddress2", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.Address2, String.Empty)),
                    new PropertyByName<Order>("ShippingZipPostalCode", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.ZipPostalCode, String.Empty)),
                    new PropertyByName<Order>("ShippingPhoneNumber", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.PhoneNumber, String.Empty)),
                    new PropertyByName<Order>("ShippingFaxNumber", p => p.ShippingAddress.Return(shippingAddress => shippingAddress.FaxNumber, String.Empty))
            };

            return ExportToXlsx(properties, orders);
        }

        /// <summary>
        /// Export customer list to XLSX
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="customers">Customers</param>
        public virtual byte[] ExportCustomersToXlsx(IList<Customer> customers)
        {
            //property array
            var properties = new[]
            {
                new PropertyByName<Customer>("CustomerId", p => p.Id),
                new PropertyByName<Customer>("CustomerGuid", p => p.CustomerGuid),
                new PropertyByName<Customer>("Email", p => p.Email),
                new PropertyByName<Customer>("Username", p => p.Username),
                new PropertyByName<Customer>("Password", p => p.Password),
                new PropertyByName<Customer>("PasswordFormatId", p => p.PasswordFormatId),
                new PropertyByName<Customer>("PasswordSalt", p => p.PasswordSalt),
                new PropertyByName<Customer>("IsTaxExempt", p => p.IsTaxExempt),
                new PropertyByName<Customer>("AffiliateId", p => p.AffiliateId),
                new PropertyByName<Customer>("VendorId", p => p.VendorId),
                new PropertyByName<Customer>("Active", p => p.Active),
                new PropertyByName<Customer>("IsGuest", p => p.IsGuest()),
                new PropertyByName<Customer>("IsRegistered", p => p.IsRegistered()),
                new PropertyByName<Customer>("IsAdministrator", p => p.IsAdmin()),
                new PropertyByName<Customer>("IsForumModerator", p => p.IsForumModerator()),
                //attributes
                new PropertyByName<Customer>("FirstName", p => p.GetAttribute<string>(SystemCustomerAttributeNames.FirstName)),
                new PropertyByName<Customer>("LastName", p => p.GetAttribute<string>(SystemCustomerAttributeNames.LastName)),
                new PropertyByName<Customer>("Gender", p => p.GetAttribute<string>(SystemCustomerAttributeNames.Gender)),
                new PropertyByName<Customer>("Company", p => p.GetAttribute<string>(SystemCustomerAttributeNames.Company)),
                new PropertyByName<Customer>("StreetAddress", p => p.GetAttribute<string>(SystemCustomerAttributeNames.StreetAddress)),
                new PropertyByName<Customer>("StreetAddress2", p => p.GetAttribute<string>(SystemCustomerAttributeNames.StreetAddress2)),
                new PropertyByName<Customer>("ZipPostalCode", p => p.GetAttribute<string>(SystemCustomerAttributeNames.ZipPostalCode)),
                new PropertyByName<Customer>("City", p => p.GetAttribute<string>(SystemCustomerAttributeNames.City)),
                new PropertyByName<Customer>("CountryId", p => p.GetAttribute<int>(SystemCustomerAttributeNames.CountryId)),
                new PropertyByName<Customer>("StateProvinceId", p => p.GetAttribute<int>(SystemCustomerAttributeNames.StateProvinceId)),
                new PropertyByName<Customer>("Phone", p => p.GetAttribute<string>(SystemCustomerAttributeNames.Phone)),
                new PropertyByName<Customer>("Fax", p => p.GetAttribute<string>(SystemCustomerAttributeNames.Fax)),
                new PropertyByName<Customer>("VatNumber", p => p.GetAttribute<string>(SystemCustomerAttributeNames.VatNumber)),
                new PropertyByName<Customer>("VatNumberStatusId", p => p.GetAttribute<int>(SystemCustomerAttributeNames.VatNumberStatusId)),
                new PropertyByName<Customer>("TimeZoneId", p => p.GetAttribute<string>(SystemCustomerAttributeNames.TimeZoneId)),
                new PropertyByName<Customer>("AvatarPictureId", p => p.GetAttribute<int>(SystemCustomerAttributeNames.AvatarPictureId)),
                new PropertyByName<Customer>("ForumPostCount", p => p.GetAttribute<int>(SystemCustomerAttributeNames.ForumPostCount)),
                new PropertyByName<Customer>("Signature", p => p.GetAttribute<string>(SystemCustomerAttributeNames.Signature)),
            };

            return ExportToXlsx(properties, customers);
        }

        /// <summary>
        /// Export customer list to xml
        /// </summary>
        /// <param name="customers">Customers</param>
        /// <returns>Result in XML format</returns>
        public virtual string ExportCustomersToXml(IList<Customer> customers)
        {
            var sb = new StringBuilder();
            var stringWriter = new StringWriter(sb);
            var xmlWriter = new XmlTextWriter(stringWriter);
            xmlWriter.WriteStartDocument();
            xmlWriter.WriteStartElement("Customers");
            xmlWriter.WriteAttributeString("Version", NopVersion.CurrentVersion);

            foreach (var customer in customers)
            {
                xmlWriter.WriteStartElement("Customer");
                xmlWriter.WriteElementString("CustomerId", null, customer.Id.ToString());
                xmlWriter.WriteElementString("CustomerGuid", null, customer.CustomerGuid.ToString());
                xmlWriter.WriteElementString("Email", null, customer.Email);
                xmlWriter.WriteElementString("Username", null, customer.Username);
                xmlWriter.WriteElementString("Password", null, customer.Password);
                xmlWriter.WriteElementString("PasswordFormatId", null, customer.PasswordFormatId.ToString());
                xmlWriter.WriteElementString("PasswordSalt", null, customer.PasswordSalt);
                xmlWriter.WriteElementString("IsTaxExempt", null, customer.IsTaxExempt.ToString());
                xmlWriter.WriteElementString("AffiliateId", null, customer.AffiliateId.ToString());
                xmlWriter.WriteElementString("VendorId", null, customer.VendorId.ToString());
                xmlWriter.WriteElementString("Active", null, customer.Active.ToString());

                xmlWriter.WriteElementString("IsGuest", null, customer.IsGuest().ToString());
                xmlWriter.WriteElementString("IsRegistered", null, customer.IsRegistered().ToString());
                xmlWriter.WriteElementString("IsAdministrator", null, customer.IsAdmin().ToString());
                xmlWriter.WriteElementString("IsForumModerator", null, customer.IsForumModerator().ToString());

                xmlWriter.WriteElementString("FirstName", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.FirstName));
                xmlWriter.WriteElementString("LastName", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.LastName));
                xmlWriter.WriteElementString("Gender", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.Gender));
                xmlWriter.WriteElementString("Company", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.Company));

                xmlWriter.WriteElementString("CountryId", null, customer.GetAttribute<int>(SystemCustomerAttributeNames.CountryId).ToString());
                xmlWriter.WriteElementString("StreetAddress", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.StreetAddress));
                xmlWriter.WriteElementString("StreetAddress2", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.StreetAddress2));
                xmlWriter.WriteElementString("ZipPostalCode", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.ZipPostalCode));
                xmlWriter.WriteElementString("City", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.City));
                xmlWriter.WriteElementString("StateProvinceId", null, customer.GetAttribute<int>(SystemCustomerAttributeNames.StateProvinceId).ToString());
                xmlWriter.WriteElementString("Phone", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.Phone));
                xmlWriter.WriteElementString("Fax", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.Fax));
                xmlWriter.WriteElementString("VatNumber", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.VatNumber));
                xmlWriter.WriteElementString("VatNumberStatusId", null, customer.GetAttribute<int>(SystemCustomerAttributeNames.VatNumberStatusId).ToString());
                xmlWriter.WriteElementString("TimeZoneId", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.TimeZoneId));

                foreach (var store in _storeService.GetAllStores())
                {
                    var newsletter = _newsLetterSubscriptionService.GetNewsLetterSubscriptionByEmailAndStoreId(customer.Email, store.Id);
                    bool subscribedToNewsletters = newsletter != null && newsletter.Active;
                    xmlWriter.WriteElementString(string.Format("Newsletter-in-store-{0}", store.Id), null, subscribedToNewsletters.ToString());
                }

                xmlWriter.WriteElementString("AvatarPictureId", null, customer.GetAttribute<int>(SystemCustomerAttributeNames.AvatarPictureId).ToString());
                xmlWriter.WriteElementString("ForumPostCount", null, customer.GetAttribute<int>(SystemCustomerAttributeNames.ForumPostCount).ToString());
                xmlWriter.WriteElementString("Signature", null, customer.GetAttribute<string>(SystemCustomerAttributeNames.Signature));

                xmlWriter.WriteEndElement();
            }

            xmlWriter.WriteEndElement();
            xmlWriter.WriteEndDocument();
            xmlWriter.Close();
            return stringWriter.ToString();
        }

        /// <summary>
        /// Export newsletter subscribers to TXT
        /// </summary>
        /// <param name="subscriptions">Subscriptions</param>
        /// <returns>Result in TXT (string) format</returns>
        public virtual string ExportNewsletterSubscribersToTxt(IList<NewsLetterSubscription> subscriptions)
        {
            if (subscriptions == null)
                throw new ArgumentNullException("subscriptions");

            const string separator = ",";
            var sb = new StringBuilder();
            foreach (var subscription in subscriptions)
            {
                sb.Append(subscription.Email);
                sb.Append(separator);
                sb.Append(subscription.Active);
                sb.Append(separator);
                sb.Append(subscription.StoreId);
                sb.Append(Environment.NewLine); //new line
            }
            return sb.ToString();
        }

        /// <summary>
        /// Export states to TXT
        /// </summary>
        /// <param name="states">States</param>
        /// <returns>Result in TXT (string) format</returns>
        public virtual string ExportStatesToTxt(IList<StateProvince> states)
        {
            if (states == null)
                throw new ArgumentNullException("states");

            const string separator = ",";
            var sb = new StringBuilder();
            foreach (var state in states)
            {
                sb.Append(state.Country.TwoLetterIsoCode);
                sb.Append(separator);
                sb.Append(state.Name);
                sb.Append(separator);
                sb.Append(state.Abbreviation);
                sb.Append(separator);
                sb.Append(state.Published);
                sb.Append(separator);
                sb.Append(state.DisplayOrder);
                sb.Append(Environment.NewLine); //new line
            }
            return sb.ToString();
        }

        #endregion
    }
}
