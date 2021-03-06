using System;
using System.Drawing;
using System.Diagnostics;
using Bit.App.Abstractions;
using Bit.App.Repositories;
using Bit.App.Services;
using Bit.iOS.Core.Services;
using Foundation;
using Microsoft.Practices.Unity;
using UIKit;
using XLabs.Ioc;
using XLabs.Ioc.Unity;
using Bit.iOS.Core;
using Newtonsoft.Json;
using Bit.iOS.Extension.Models;
using MobileCoreServices;
using Plugin.Settings.Abstractions;
using Plugin.Connectivity;
using Plugin.Fingerprint;
using Bit.iOS.Core.Utilities;
using Bit.App.Resources;
using Bit.iOS.Core.Controllers;

namespace Bit.iOS.Extension
{
    public partial class LoadingViewController : ExtendedUIViewController
    {
        private Context _context = new Context();
        private bool _setupHockeyApp = false;
        private readonly JsonSerializerSettings _jsonSettings =
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        private IGoogleAnalyticsService _googleAnalyticsService;
        private ISettings _settings;

        public LoadingViewController(IntPtr handle) : base(handle)
        { }

        public override void ViewDidLoad()
        {
            SetIoc();
            SetCulture();

            base.ViewDidLoad();
            View.BackgroundColor = new UIColor(red: 0.94f, green: 0.94f, blue: 0.96f, alpha: 1.0f);
            _context.ExtContext = ExtensionContext;
            _googleAnalyticsService = Resolver.Resolve<IGoogleAnalyticsService>();
            _settings = Resolver.Resolve<ISettings>();

            if(!_setupHockeyApp)
            {
                var appIdService = Resolver.Resolve<IAppIdService>();
                var crashManagerDelegate = new HockeyAppCrashManagerDelegate(appIdService, Resolver.Resolve<IAuthService>());
                var manager = HockeyApp.iOS.BITHockeyManager.SharedHockeyManager;
                manager.Configure("51f96ae568ba45f699a18ad9f63046c3", crashManagerDelegate);
                manager.CrashManager.CrashManagerStatus = HockeyApp.iOS.BITCrashManagerStatus.AutoSend;
                manager.UserId = appIdService.AppId;
                manager.StartManager();
                manager.Authenticator.AuthenticateInstallation();
                _setupHockeyApp = true;
            }

            foreach(var item in ExtensionContext.InputItems)
            {
                var processed = false;
                foreach(var itemProvider in item.Attachments)
                {
                    if(ProcessWebUrlProvider(itemProvider)
                        || ProcessFindLoginProvider(itemProvider)
                        || ProcessFindLoginBrowserProvider(itemProvider, Constants.UTTypeAppExtensionFillBrowserAction)
                        || ProcessFindLoginBrowserProvider(itemProvider, Constants.UTTypeAppExtensionFillWebViewAction)
                        || ProcessSaveLoginProvider(itemProvider)
                        || ProcessChangePasswordProvider(itemProvider)
                        || ProcessExtensionSetupProvider(itemProvider))
                    {
                        processed = true;
                        break;
                    }
                }

                if(processed)
                {
                    break;
                }
            }
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);

            var authService = Resolver.Resolve<IAuthService>();
            if(!authService.IsAuthenticated)
            {
                var alert = Dialogs.CreateAlert(null, AppResources.MustLogInMainApp, AppResources.Ok, (a) =>
                {
                    CompleteRequest(null);
                });
                PresentViewController(alert, true, null);
                return;
            }

            var lockService = Resolver.Resolve<ILockService>();
            var lockType = lockService.GetLockType(false);
            switch(lockType)
            {
                case App.Enums.LockType.Fingerprint:
                    PerformSegue("lockFingerprintSegue", this);
                    break;
                case App.Enums.LockType.PIN:
                    PerformSegue("lockPinSegue", this);
                    break;
                case App.Enums.LockType.Password:
                    PerformSegue("lockPasswordSegue", this);
                    break;
                default:
                    ContinueOn();
                    break;
            }
        }

        public override void PrepareForSegue(UIStoryboardSegue segue, NSObject sender)
        {
            var navController = segue.DestinationViewController as UINavigationController;
            if(navController != null)
            {
                var listLoginController = navController.TopViewController as LoginListViewController;
                var addLoginController = navController.TopViewController as LoginAddViewController;
                var fingerprintViewController = navController.TopViewController as LockFingerprintViewController;
                var pinViewController = navController.TopViewController as LockPinViewController;
                var passwordViewController = navController.TopViewController as LockPasswordViewController;
                var setupViewController = navController.TopViewController as SetupViewController;

                if(listLoginController != null)
                {
                    listLoginController.Context = _context;
                    listLoginController.LoadingController = this;
                }
                else if(addLoginController != null)
                {
                    addLoginController.Context = _context;
                    addLoginController.LoadingController = this;
                }
                else if(fingerprintViewController != null)
                {
                    fingerprintViewController.Context = _context;
                    fingerprintViewController.LoadingController = this;
                }
                else if(pinViewController != null)
                {
                    pinViewController.Context = _context;
                    pinViewController.LoadingController = this;
                }
                else if(passwordViewController != null)
                {
                    passwordViewController.Context = _context;
                    passwordViewController.LoadingController = this;
                }
                else if(setupViewController != null)
                {
                    setupViewController.Context = _context;
                    setupViewController.LoadingController = this;
                }
            }
        }

        public void DismissLockAndContinue()
        {
            Debug.WriteLine("BW Log, Dismissing lock controller.");
            DismissViewController(false, () =>
            {
                ContinueOn();
            });
        }

        private void ContinueOn()
        {
            Debug.WriteLine("BW Log, Segue to setup, login add or list.");
            _settings.AddOrUpdateValue(App.Constants.LastActivityDate, DateTime.UtcNow);

            if(_context.ProviderType == Constants.UTTypeAppExtensionSaveLoginAction)
            {
                PerformSegue("newLoginSegue", this);
            }
            else if(_context.ProviderType == Constants.UTTypeAppExtensionSetup)
            {
                PerformSegue("setupSegue", this);
            }
            else
            {
                PerformSegue("loginListSegue", this);
            }
        }

        public void CompleteUsernamePasswordRequest(string username, string password)
        {
            NSDictionary itemData = null;
            if(_context.ProviderType == UTType.PropertyList)
            {
                var fillScript = new FillScript(_context.Details, username, password);
                var scriptJson = JsonConvert.SerializeObject(fillScript, _jsonSettings);
                var scriptDict = new NSDictionary(Constants.AppExtensionWebViewPageFillScript, scriptJson);
                itemData = new NSDictionary(NSJavaScriptExtension.FinalizeArgumentKey, scriptDict);
            }
            else if(_context.ProviderType == Constants.UTTypeAppExtensionFindLoginAction)
            {
                itemData = new NSDictionary(
                    Constants.AppExtensionUsernameKey, username,
                    Constants.AppExtensionPasswordKey, password);
            }
            else if(_context.ProviderType == Constants.UTTypeAppExtensionFillBrowserAction
                || _context.ProviderType == Constants.UTTypeAppExtensionFillWebViewAction)
            {
                var fillScript = new FillScript(_context.Details, username, password);
                var scriptJson = JsonConvert.SerializeObject(fillScript, _jsonSettings);
                itemData = new NSDictionary(Constants.AppExtensionWebViewPageFillScript, scriptJson);
            }
            else if(_context.ProviderType == Constants.UTTypeAppExtensionSaveLoginAction)
            {
                itemData = new NSDictionary(
                    Constants.AppExtensionUsernameKey, username,
                    Constants.AppExtensionPasswordKey, password);
            }
            else if(_context.ProviderType == Constants.UTTypeAppExtensionChangePasswordAction)
            {
                itemData = new NSDictionary(
                    Constants.AppExtensionPasswordKey, string.Empty,
                    Constants.AppExtensionOldPasswordKey, password);
            }

            CompleteRequest(itemData);
        }

        public void CompleteRequest(NSDictionary itemData)
        {
            Debug.WriteLine("BW LOG, itemData: " + itemData);

            var resultsProvider = new NSItemProvider(itemData, UTType.PropertyList);
            var resultsItem = new NSExtensionItem { Attachments = new NSItemProvider[] { resultsProvider } };
            var returningItems = new NSExtensionItem[] { resultsItem };

            if(itemData != null)
            {
                _settings.AddOrUpdateValue(App.Constants.LastActivityDate, DateTime.UtcNow);
                _googleAnalyticsService.TrackExtensionEvent("AutoFilled", _context.ProviderType);
            }
            else
            {
                _googleAnalyticsService.TrackExtensionEvent("Closed", _context.ProviderType);
            }

            _googleAnalyticsService.Dispatch(() =>
            {
                NSRunLoop.Main.BeginInvokeOnMainThread(() =>
                {
                    Resolver.ResetResolver();
                    ExtensionContext.CompleteRequest(returningItems, null);
                });
            });
        }

        private void SetIoc()
        {
            var container = new UnityContainer();

            container
                // Services
                .RegisterType<IDatabaseService, DatabaseService>(new ContainerControlledLifetimeManager())
                .RegisterType<ISqlService, SqlService>(new ContainerControlledLifetimeManager())
                .RegisterType<ISecureStorageService, KeyChainStorageService>(new ContainerControlledLifetimeManager())
                .RegisterType<ICryptoService, CryptoService>(new ContainerControlledLifetimeManager())
                .RegisterType<IKeyDerivationService, CommonCryptoKeyDerivationService>(new ContainerControlledLifetimeManager())
                .RegisterType<IAuthService, AuthService>(new ContainerControlledLifetimeManager())
                .RegisterType<IFolderService, FolderService>(new ContainerControlledLifetimeManager())
                .RegisterType<ILoginService, LoginService>(new ContainerControlledLifetimeManager())
                .RegisterType<ISyncService, SyncService>(new ContainerControlledLifetimeManager())
                .RegisterType<IPasswordGenerationService, PasswordGenerationService>(new ContainerControlledLifetimeManager())
                .RegisterType<IAppIdService, AppIdService>(new ContainerControlledLifetimeManager())
                .RegisterType<ILockService, LockService>(new ContainerControlledLifetimeManager())
                .RegisterType<IGoogleAnalyticsService, GoogleAnalyticsService>(new ContainerControlledLifetimeManager())
                .RegisterType<ILocalizeService, LocalizeService>(new ContainerControlledLifetimeManager())
                .RegisterType<ILogService, LogService>(new ContainerControlledLifetimeManager())
                .RegisterType<IHttpService, HttpService>(new ContainerControlledLifetimeManager())
                // Repositories
                .RegisterType<IFolderRepository, FolderRepository>(new ContainerControlledLifetimeManager())
                .RegisterType<IFolderApiRepository, FolderApiRepository>(new ContainerControlledLifetimeManager())
                .RegisterType<ILoginRepository, LoginRepository>(new ContainerControlledLifetimeManager())
                .RegisterType<ILoginApiRepository, LoginApiRepository>(new ContainerControlledLifetimeManager())
                .RegisterType<IAuthApiRepository, AuthApiRepository>(new ContainerControlledLifetimeManager())
                // Other
                .RegisterInstance(CrossConnectivity.Current, new ContainerControlledLifetimeManager())
                .RegisterInstance(CrossFingerprint.Current, new ContainerControlledLifetimeManager());

            ISettings settings = new Settings("group.com.8bit.bitwarden");
            container.RegisterInstance(settings, new ContainerControlledLifetimeManager());

            Resolver.ResetResolver(new UnityResolver(container));
        }

        private void SetCulture()
        {
            var localizeService = Resolver.Resolve<ILocalizeService>();
            var ci = localizeService.GetCurrentCultureInfo();
            AppResources.Culture = ci;
            localizeService.SetLocale(ci);
        }

        private bool ProcessItemProvider(NSItemProvider itemProvider, string type, Action<NSDictionary> action)
        {
            if(!itemProvider.HasItemConformingTo(type))
            {
                return false;
            }

            itemProvider.LoadItem(type, null, (NSObject list, NSError error) =>
            {
                if(list == null)
                {
                    return;
                }

                _context.ProviderType = type;
                var dict = list as NSDictionary;
                action(dict);

                _googleAnalyticsService.TrackExtensionEvent("ProcessItemProvider", type);

                Debug.WriteLine("BW LOG, ProviderType: " + _context.ProviderType);
                Debug.WriteLine("BW LOG, Url: " + _context.Url);
                Debug.WriteLine("BW LOG, Title: " + _context.LoginTitle);
                Debug.WriteLine("BW LOG, Username: " + _context.Username);
                Debug.WriteLine("BW LOG, Password: " + _context.Password);
                Debug.WriteLine("BW LOG, Old Password: " + _context.OldPassword);
                Debug.WriteLine("BW LOG, Notes: " + _context.Notes);
                Debug.WriteLine("BW LOG, Details: " + _context.Details);

                if(_context.PasswordOptions != null)
                {
                    Debug.WriteLine("BW LOG, PasswordOptions Min Length: " + _context.PasswordOptions.MinLength);
                    Debug.WriteLine("BW LOG, PasswordOptions Max Length: " + _context.PasswordOptions.MaxLength);
                    Debug.WriteLine("BW LOG, PasswordOptions Require Digits: " + _context.PasswordOptions.RequireDigits);
                    Debug.WriteLine("BW LOG, PasswordOptions Require Symbols: " + _context.PasswordOptions.RequireSymbols);
                    Debug.WriteLine("BW LOG, PasswordOptions Forbidden Chars: " + _context.PasswordOptions.ForbiddenCharacters);
                }
            });

            return true;
        }

        private bool ProcessWebUrlProvider(NSItemProvider itemProvider)
        {
            return ProcessItemProvider(itemProvider, UTType.PropertyList, (dict) =>
            {
                var result = dict[NSJavaScriptExtension.PreprocessingResultsKey];
                if(result == null)
                {
                    return;
                }

                _context.Url = new Uri(result.ValueForKey(new NSString(Constants.AppExtensionUrlStringKey)) as NSString);
                var jsonStr = result.ValueForKey(new NSString(Constants.AppExtensionWebViewPageDetails)) as NSString;
                _context.Details = DeserializeString<PageDetails>(jsonStr);
            });
        }

        private bool ProcessFindLoginProvider(NSItemProvider itemProvider)
        {
            return ProcessItemProvider(itemProvider, Constants.UTTypeAppExtensionFindLoginAction, (dict) =>
            {
                var version = dict[Constants.AppExtensionVersionNumberKey] as NSNumber;
                var url = dict[Constants.AppExtensionUrlStringKey] as NSString;

                if(url != null)
                {
                    _context.Url = new Uri(url);
                }
            });
        }

        private bool ProcessFindLoginBrowserProvider(NSItemProvider itemProvider, string action)
        {
            return ProcessItemProvider(itemProvider, action, (dict) =>
            {
                var version = dict[Constants.AppExtensionVersionNumberKey] as NSNumber;
                var url = dict[Constants.AppExtensionUrlStringKey] as NSString;
                if(url != null)
                {
                    _context.Url = new Uri(url);
                }

                _context.Details = DeserializeDictionary<PageDetails>(dict[Constants.AppExtensionWebViewPageDetails] as NSDictionary);
            });
        }

        private bool ProcessSaveLoginProvider(NSItemProvider itemProvider)
        {
            return ProcessItemProvider(itemProvider, Constants.UTTypeAppExtensionSaveLoginAction, (dict) =>
            {
                var version = dict[Constants.AppExtensionVersionNumberKey] as NSNumber;
                var url = dict[Constants.AppExtensionUrlStringKey] as NSString;
                var title = dict[Constants.AppExtensionTitleKey] as NSString;
                var sectionTitle = dict[Constants.AppExtensionSectionTitleKey] as NSString;
                var username = dict[Constants.AppExtensionUsernameKey] as NSString;
                var password = dict[Constants.AppExtensionPasswordKey] as NSString;
                var notes = dict[Constants.AppExtensionNotesKey] as NSString;
                var fields = dict[Constants.AppExtensionFieldsKey] as NSDictionary;

                if(url != null)
                {
                    _context.Url = new Uri(url);
                }

                _context.LoginTitle = title;
                _context.Username = username;
                _context.Password = password;
                _context.Notes = notes;
                _context.PasswordOptions = DeserializeDictionary<PasswordGenerationOptions>(dict[Constants.AppExtensionPasswordGeneratorOptionsKey] as NSDictionary);
            });
        }

        private bool ProcessChangePasswordProvider(NSItemProvider itemProvider)
        {
            return ProcessItemProvider(itemProvider, Constants.UTTypeAppExtensionChangePasswordAction, (dict) =>
            {
                var version = dict[Constants.AppExtensionVersionNumberKey] as NSNumber;
                var url = dict[Constants.AppExtensionUrlStringKey] as NSString;
                var title = dict[Constants.AppExtensionTitleKey] as NSString;
                var sectionTitle = dict[Constants.AppExtensionSectionTitleKey] as NSString;
                var username = dict[Constants.AppExtensionUsernameKey] as NSString;
                var password = dict[Constants.AppExtensionPasswordKey] as NSString;
                var oldPassword = dict[Constants.AppExtensionOldPasswordKey] as NSString;
                var notes = dict[Constants.AppExtensionNotesKey] as NSString;
                var fields = dict[Constants.AppExtensionFieldsKey] as NSDictionary;

                if(url != null)
                {
                    _context.Url = new Uri(url);
                }

                _context.LoginTitle = title;
                _context.Username = username;
                _context.Password = password;
                _context.OldPassword = oldPassword;
                _context.Notes = notes;
                _context.PasswordOptions = DeserializeDictionary<PasswordGenerationOptions>(dict[Constants.AppExtensionPasswordGeneratorOptionsKey] as NSDictionary);
            });
        }

        private bool ProcessExtensionSetupProvider(NSItemProvider itemProvider)
        {
            if(itemProvider.HasItemConformingTo(Constants.UTTypeAppExtensionSetup))
            {
                _context.ProviderType = Constants.UTTypeAppExtensionSetup;
                return true;
            }

            return false;
        }

        private T DeserializeDictionary<T>(NSDictionary dict)
        {
            if(dict != null)
            {
                NSError jsonError;
                var jsonData = NSJsonSerialization.Serialize(dict, NSJsonWritingOptions.PrettyPrinted, out jsonError);
                if(jsonData != null)
                {
                    var jsonString = new NSString(jsonData, NSStringEncoding.UTF8);
                    return DeserializeString<T>(jsonString);
                }
            }

            return default(T);
        }

        private T DeserializeString<T>(NSString jsonString)
        {
            if(jsonString != null)
            {
                var convertedObject = JsonConvert.DeserializeObject<T>(jsonString.ToString());
                return convertedObject;
            }

            return default(T);
        }
    }
}