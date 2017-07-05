using System;
using System.Collections.Generic;
using FluentValidation;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Util;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;
using IScriptableViewModel = Octopus.Manager.Tentacle.Infrastructure.IScriptableViewModel;

namespace Octopus.Manager.Tentacle.Proxy
{
    public class PollingProxyWizardModel : ProxyWizardModel
    {
        public PollingProxyWizardModel(string selectedInstance, ApplicationName application) : base(selectedInstance, application)
        {
            UseNoProxy = true;
        }

        protected override string Command => "polling-proxy";
        public override string Header => "Polling Proxy";
        public override string Title => "Polling Tentacle Proxy Settings";
        public override string Description => "Settings for the proxy that this Tentacle will use to connect to the Octopus server.";
    }

    public class ProxyWizardModel : ViewModel, IScriptableViewModel
    {
        string proxyUsername;
        string proxyPassword;
        bool useNoProxy;
        bool useDefaultProxy;
        bool useDefaultProxyCustomCredentials;
        bool useCustomProxy;
        bool showProxySettings;
        string proxyServerHost;
        int proxyServerPort;

        public ProxyWizardModel(string selectedInstance, ApplicationName application)
        {
            UseDefaultProxy = true;
            ToggleService = true;
            ProxyServerPort = 80;

            InstanceName = selectedInstance;
            Executable = CommandLine.PathToTentacleExe();

            Validator = CreateValidator();
        }

        protected virtual string Command => "proxy";
        public virtual string Header => "Web Proxy";
        public virtual string Title => "Web Request Proxy Settings";
        public virtual string Description => "Settings for the proxy that Octopus will use to make web requests.";

        public string InstanceName { get; set; }

        public bool ToggleService { get; set; }

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

        public bool UseNoProxy
        {
            get => useNoProxy;
            set
            {
                if (value.Equals(useNoProxy)) return;
                ClearOption();
                useNoProxy = value;
                OnPropertyChanged();
            }
        }

        public bool UseDefaultProxy
        {
            get => useDefaultProxy;
            set
            {
                if (value.Equals(useDefaultProxy)) return;
                ClearOption();
                useDefaultProxy = value;
                OnPropertyChanged();
            }
        }

        public bool UseDefaultProxyCustomCredentials
        {
            get => useDefaultProxyCustomCredentials;
            set
            {
                if (value.Equals(useDefaultProxyCustomCredentials)) return;
                ClearOption();
                useDefaultProxyCustomCredentials = value;
                OnPropertyChanged();
            }
        }

        public bool UseCustomProxy
        {
            get => useCustomProxy;
            set
            {
                if (value.Equals(useCustomProxy)) return;
                ClearOption();
                useCustomProxy = value;
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

        void ClearOption()
        {
            useNoProxy = false;
            useDefaultProxy = false;
            useDefaultProxyCustomCredentials = false;
            useCustomProxy = false;
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

            if (useNoProxy)
            {
                yield return Cli(Command)
                    .Argument("proxyEnable", false)
                    .Argument("proxyUsername", string.Empty)
                    .Argument("proxyPassword", string.Empty)
                    .Argument("proxyHost", string.Empty)
                    .Argument("proxyPort", string.Empty)
                    .Build();
            }
            else if (UseDefaultProxy)
            {
                // Explicitly clear the credentials
                yield return Cli(Command)
                    .Argument("proxyEnable", true)
                    .Argument("proxyUsername", string.Empty)
                    .Argument("proxyPassword", string.Empty)
                    .Argument("proxyHost", string.Empty)
                    .Argument("proxyPort", string.Empty)
                    .Build();
            }
            else if (UseDefaultProxyCustomCredentials)
            {
                yield return Cli(Command)
                    .Argument("proxyEnable", true)
                    .Argument("proxyUsername", ProxyUsername)
                    .Argument("proxyPassword", ProxyPassword)
                    .Argument("proxyHost", string.Empty)
                    .Argument("proxyPort", string.Empty)
                    .Build();
            }
            else if (UseCustomProxy)
            {
                var host = new UriBuilder(ProxyServerHost).Host;

                yield return Cli(Command)
                    .Argument("proxyEnable", true)
                    .Argument("proxyUsername", ProxyUsername)
                    .Argument("proxyPassword", ProxyPassword)
                    .Argument("proxyHost", host)
                    .Argument("proxyPort", ProxyServerPort)
                    .Build();
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
                validator.When(r => r.UseDefaultProxyCustomCredentials, delegate
                {
                    validator.RuleFor(m => m.ProxyUsername).NotEmpty().WithMessage("Please enter a username for the proxy server");
                    validator.RuleFor(m => m.ProxyPassword).NotEmpty().WithMessage("Please enter a password for the proxy server");
                });
                validator.When(r => r.UseCustomProxy, delegate
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
    }
}