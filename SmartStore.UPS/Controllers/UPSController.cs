﻿using System;
using System.Text;
using System.Web.Mvc;
using SmartStore.Services.Configuration;
using SmartStore.Services.Directory;
using SmartStore.UPS.Domain;
using SmartStore.UPS.Models;
using SmartStore.Utilities;
using SmartStore.Web.Framework.Controllers;
using SmartStore.Web.Framework.Security;

namespace SmartStore.UPS.Controllers
{
    [AdminAuthorize]
    public class UPSController : PluginControllerBase
    {
        private readonly UPSSettings _upsSettings;
        private readonly ISettingService _settingService;
        private readonly ICountryService _countryService;

        public UPSController(UPSSettings upsSettings, ISettingService settingService,
            ICountryService countryService)
        {
            _upsSettings = upsSettings;
            _settingService = settingService;
            _countryService = countryService;
        }

        public ActionResult Configure()
        {
            var model = new UPSModel();
            model.Url = _upsSettings.Url;
            model.AccessKey = _upsSettings.AccessKey;
            model.Username = _upsSettings.Username;
            model.Password = _upsSettings.Password;
            model.AdditionalHandlingCharge = _upsSettings.AdditionalHandlingCharge;
            model.InsurePackage = _upsSettings.InsurePackage;
            model.PrimaryStoreCurrencyCode = Services.StoreContext.CurrentStore.PrimaryStoreCurrency.CurrencyCode;

            foreach (UPSCustomerClassification customerClassification in Enum.GetValues(typeof(UPSCustomerClassification)))
            {
                model.AvailableCustomerClassifications.Add(new SelectListItem
                {
                    Text = Inflector.Titleize(customerClassification.ToString()),
                    Value = customerClassification.ToString(),
                    Selected = customerClassification == _upsSettings.CustomerClassification
                });
            }
            foreach (UPSPickupType pickupType in Enum.GetValues(typeof(UPSPickupType)))
            {
                model.AvailablePickupTypes.Add(new SelectListItem
                {
                    Text = Inflector.Titleize(pickupType.ToString()),
                    Value = pickupType.ToString(),
                    Selected = pickupType == _upsSettings.PickupType
                });
            }
            foreach (UPSPackagingType packagingType in Enum.GetValues(typeof(UPSPackagingType)))
            {
                model.AvailablePackagingTypes.Add(new SelectListItem
                {
                    Text = Inflector.Titleize(packagingType.ToString()),
                    Value = packagingType.ToString(),
                    Selected = packagingType == _upsSettings.PackagingType
                });
            }

            foreach (var country in _countryService.GetAllCountries(true))
            {
                model.AvailableCountries.Add(new SelectListItem
                {
                    Text = country.Name.ToString(),
                    Value = country.Id.ToString(),
                    Selected = country.Id == _upsSettings.DefaultShippedFromCountryId
                });
            }
            model.DefaultShippedFromCountryId = _upsSettings.DefaultShippedFromCountryId;
            model.DefaultShippedFromZipPostalCode = _upsSettings.DefaultShippedFromZipPostalCode;

            // Load Domestic service names
            var services = new UPSServices();
            var carrierServicesOfferedDomestic = _upsSettings.CarrierServicesOffered;
            foreach (string service in services.Services)
            {
                model.AvailableCarrierServices.Add(service);
            }

            if (!String.IsNullOrEmpty(carrierServicesOfferedDomestic))
            {
                foreach (string service in services.Services)
                {
                    var serviceId = UPSServices.GetServiceId(service);
                    if (!String.IsNullOrEmpty(serviceId) && !String.IsNullOrEmpty(carrierServicesOfferedDomestic))
                    {
                        // Add delimiters [] so that single digit IDs aren't found in multi-digit IDs
                        if (carrierServicesOfferedDomestic.Contains(String.Format("[{0}]", serviceId)))
                        {
                            model.CarrierServicesOffered.Add(service);
                        }
                    }
                }
            }

            return View(model);
        }

        [HttpPost]
        public ActionResult Configure(UPSModel model)
        {
            if (!ModelState.IsValid)
            {
                return Configure();
            }

            //save settings
            _upsSettings.Url = model.Url;
            _upsSettings.AccessKey = model.AccessKey.TrimSafe();
            _upsSettings.Username = model.Username.TrimSafe();
            _upsSettings.Password = model.Password.TrimSafe();
            _upsSettings.AdditionalHandlingCharge = model.AdditionalHandlingCharge;
            _upsSettings.InsurePackage = model.InsurePackage;
            _upsSettings.CustomerClassification = (UPSCustomerClassification)Enum.Parse(typeof(UPSCustomerClassification), model.CustomerClassification);
            _upsSettings.PickupType = (UPSPickupType)Enum.Parse(typeof(UPSPickupType), model.PickupType);
            _upsSettings.PackagingType = (UPSPackagingType)Enum.Parse(typeof(UPSPackagingType), model.PackagingType);
            _upsSettings.DefaultShippedFromCountryId = model.DefaultShippedFromCountryId;
            _upsSettings.DefaultShippedFromZipPostalCode = model.DefaultShippedFromZipPostalCode;


            // Save selected services
            var carrierServicesOfferedDomestic = new StringBuilder();
            int carrierServicesDomesticSelectedCount = 0;
            if (model.CheckedCarrierServices != null)
            {
                foreach (var cs in model.CheckedCarrierServices)
                {
                    carrierServicesDomesticSelectedCount++;
                    string serviceId = UPSServices.GetServiceId(cs);
                    if (!String.IsNullOrEmpty(serviceId))
                    {
                        // Add delimiters [] so that single digit IDs aren't found in multi-digit IDs
                        carrierServicesOfferedDomestic.AppendFormat("[{0}]:", serviceId);
                    }
                }
            }
            // Add default options if no services were selected (Priority Mail International, First-Class Mail International Package, and Express Mail International)
            if (carrierServicesDomesticSelectedCount == 0)
                _upsSettings.CarrierServicesOffered = "[03]:[12]:[11]:[08]:";
            else
                _upsSettings.CarrierServicesOffered = carrierServicesOfferedDomestic.ToString();

            _settingService.SaveSetting(_upsSettings);

            return RedirectToConfiguration("SmartStore.UPS");
        }
    }
}
