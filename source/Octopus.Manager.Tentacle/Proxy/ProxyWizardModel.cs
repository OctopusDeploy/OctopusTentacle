using System;
using System.Collections.Generic;
using FluentValidation;
using Octopus.Tentacle.Core.Diagnostics;
using Octopus.Manager.Tentacle.Controls;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Shell;
using Octopus.Manager.Tentacle.Util;
using Octopus.Tentacle.Configuration;
using Octopus.Tentacle.Util;
using IScriptableViewModel = Octopus.Manager.Tentacle.Infrastructure.IScriptableViewModel;

namespace Octopus.Manager.Tentacle.Proxy
{
    public enum ProxyConfigType
    {
        NoProxy,
        DefaultProxy,
        DefaultProxyCustomCredentials,
        CustomProxy,
    }
    public class PollingProxyWizardModel : ProxyWizardModel
    {
        public PollingProxyWizardModel(InstanceSelectionModel instanceSelectionModel) : base(instanceSelectionModel)
        {
            ProxyConfigType = ProxyConfigType.NoProxy;
        }

        protected override string Command => "polling-proxy";
        public override string Header => "Polling Proxy";
        public override string Title => "Polling Tentacle Proxy Settings";
        public override string Description => "Select the proxy mode for the Tentacle to use to connect to the Octopus Server.";
    }

    public class ProxyWizardModel : ViewModel, IScriptableViewModel
    {
        ProxyConfigType proxyConfigType;
        string proxyUsername;
        string proxyPassword;
        bool showProxySettings;
        string proxyServerHost;
        int proxyServerPort;

        public ProxyWizardModel(InstanceSelectionModel instanceSelectionModel)
        {
            ProxyConfigTypes = new List<KeyValuePair<ProxyConfigType, string>>();
            ProxyConfigTypes.Add(new KeyValuePair<ProxyConfigType, string>(ProxyConfigType.NoProxy, "No Proxy"));
            ProxyConfigTypes.Add(new KeyValuePair<ProxyConfigType, string>(ProxyConfigType.DefaultProxy, "Default Proxy"));
            ProxyConfigTypes.Add(new KeyValuePair<ProxyConfigType, string>(ProxyConfigType.DefaultProxyCustomCredentials, "Default Proxy With Custom Credentials"));
            ProxyConfigTypes.Add(new KeyValuePair<ProxyConfigType, string>(ProxyConfigType.CustomProxy, "Custom Proxy"));

            InstanceSelectionModel = instanceSelectionModel;
            InstanceName = instanceSelectionModel.SelectedInstance;
            Executable = CommandLine.PathToTentacleExe();
            var serviceWatcher = new ServiceWatcher(ApplicationName.Tentacle, InstanceName, Executable);
            ProxyConfigType = ProxyConfigType.DefaultProxy;
            ToggleService = serviceWatcher.IsRunning;
            ProxyServerPort = 80;

            Validator = CreateValidator();
        }

        protected virtual string Command => "proxy";
        public virtual string Header => "Web Proxy";
        public virtual string Title => "Web Request Proxy Settings";
        public virtual string Description => "Settings for the proxy that Octopus will use to make web requests.";

        public string InstanceName { get; }

        public bool ToggleService { get; set; }
        
        public InstanceSelectionModel InstanceSelectionModel { get; }

        public List<KeyValuePair<ProxyConfigType, string>> ProxyConfigTypes { get; }

        public ProxyConfigType ProxyConfigType
        {
            get => proxyConfigType;
            set
            {
                if (value.Equals(proxyConfigType)) return;
                proxyConfigType = value;
                OnPropertyChanged();
            }
        }

        public string ProxyUsername
        {
            get => proxyUsername;
            set
            {
                if (value == proxyUsername) return;
                proxyUsername = value;
                OnPropertyChanged();
            }
        }

        public string ProxyPassword
        {
            get => proxyPassword;
            set
            {
                if (value == proxyPassword) return;
                proxyPassword = value;
                OnPropertyChanged();
            }
        }

        public string ProxyServerHost
        {
            get => proxyServerHost;
            set
            {
                if (value.Equals(proxyServerHost)) return;
                proxyServerHost = value;
                OnPropertyChanged();
            }
        }

        public int ProxyServerPort
        {
            get => proxyServerPort;
            set
            {
                if (value.Equals(proxyServerPort)) return;
                proxyServerPort = value;
                OnPropertyChanged();
            }
        }

        public bool ShowProxySettings
        {
            get => showProxySettings;
            set
            {
                if (value.Equals(showProxySettings)) return;
                showProxySettings = value;
                OnPropertyChanged();
            }
        }

        public string Executable { get; set; }

        CliBuilder Cli(string action)
        {
            return CliBuilder.ForTool(Executable, action, InstanceName);
        }

        public IEnumerable<CommandLineInvocation> GenerateScript()
        {
            if (ToggleService)
            {
                yield return Cli("service").Flag("stop").Build();
            }

            switch (ProxyConfigType)
            {
                case ProxyConfigType.NoProxy:
                    yield return Cli(Command)
                        .Argument("proxyEnable", false)
                        .Argument("proxyUsername", string.Empty)
                        .Argument("proxyPassword", string.Empty)
                        .Argument("proxyHost", string.Empty)
                        .Argument("proxyPort", string.Empty)
                        .Build();
                    break;
                case ProxyConfigType.DefaultProxy:
                    // Explicitly clear the credentials
                    yield return Cli(Command)
                        .Argument("proxyEnable", true)
                        .Argument("proxyUsername", string.Empty)
                        .Argument("proxyPassword", string.Empty)
                        .Argument("proxyHost", string.Empty)
                        .Argument("proxyPort", string.Empty)
                        .Build();
                    break;
                case ProxyConfigType.DefaultProxyCustomCredentials:
                    yield return Cli(Command)
                        .Argument("proxyEnable", true)
                        .Argument("proxyUsername", ProxyUsername)
                        .Argument("proxyPassword", ProxyPassword)
                        .Argument("proxyHost", string.Empty)
                        .Argument("proxyPort", string.Empty)
                        .Build();
                    break;
                case ProxyConfigType.CustomProxy:
                {
                    var host = new UriBuilder(ProxyServerHost).Host;

                    yield return Cli(Command)
                        .Argument("proxyEnable", true)
                        .Argument("proxyUsername", ProxyUsername)
                        .Argument("proxyPassword", ProxyPassword)
                        .Argument("proxyHost", host)
                        .Argument("proxyPort", ProxyServerPort)
                        .Build();
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (ToggleService)
            {
                yield return Cli("service").Flag("start").Build();
            }
        }

        public IEnumerable<CommandLineInvocation> GenerateRollbackScript()
        {
            yield break;
        }

        static IValidator CreateValidator()
        {
            var validator = new InlineValidator<ProxyWizardModel>();

            validator.RuleSet("ProxySettings", delegate
            {
                validator.When(r => r.ProxyConfigType == ProxyConfigType.DefaultProxyCustomCredentials, delegate
                {
                    validator.RuleFor(m => m.ProxyUsername).NotEmpty().WithMessage("Please enter a username for the proxy server");
                    validator.RuleFor(m => m.ProxyPassword).NotEmpty().WithMessage("Please enter a password for the proxy server");
                });
                validator.When(r => r.ProxyConfigType == ProxyConfigType.CustomProxy, delegate
                {
                    validator.RuleFor(m => m.ProxyServerHost).NotEmpty().WithMessage("Please enter a host name for the proxy server");
                    validator.RuleFor(m => m.ProxyServerPort).InclusiveBetween(1, 65535).WithMessage("Please enter valid port number for the proxy server");
                });
            });

            return validator;
        }

        bool NotContainPortNumber(string host)
        {
            return true;
        }

        bool NotContainHttp(string host)
        {
            return true;
        }

        public void ContributeSensitiveValues(ILog log)
        {
            if (proxyPassword != null)
                log.WithSensitiveValue(proxyPassword);
        }
    }
}