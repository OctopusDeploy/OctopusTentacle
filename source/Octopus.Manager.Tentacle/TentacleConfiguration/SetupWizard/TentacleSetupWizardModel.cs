using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentValidation;
using Octopus.Client;
using Octopus.Client.Exceptions;
using Octopus.Client.Model;
using Octopus.Diagnostics;
using Octopus.Manager.Tentacle.Infrastructure;
using Octopus.Manager.Tentacle.Proxy;
using Octopus.Manager.Tentacle.Util;
using Octopus.Shared.Configuration;
using Octopus.Shared.Util;
using Octopus.Tentacle.Configuration;

namespace Octopus.Manager.Tentacle.TentacleConfiguration.SetupWizard
{
    public enum MachineType
    {
        DeploymentTarget,
        Worker
    }

    public class TentacleSetupWizardModel : ViewModel, IScriptableViewModel, IHaveServices
    {
        readonly ApplicationName applicationName;
        CommunicationStyle communicationStyle;
        MachineType machineType;
        string octopusServerUrl;
        bool useUsernamePasswordAuthMode;
        bool useApiKeyAuthMode;
        string username;
        string password;
        string apiKey;
        bool haveCredentialsBeenVerified;
        string selectedRoles;
        string selectedEnvironment;
        string selectedTenantTags;
        string selectedTenants;
        string selectedMachinePolicy;
        string selectedWorkerPool;
        string[] potentialEnvironments;
        string[] potentialRoles;
        string[] potentialMachinePolicies;
        string[] potentialTenantTags;
        string[] potentialTenants;
        string[] potentialWorkerPools;
        string machineName;
        string homeDirectory;
        string applicationInstallDirectory;
        string pathToConfig;
        OctopusServerConfiguration handshake;
        string listenPort;
        string octopusThumbprint;
        string serverCommsPort;
        string serverWebSocket;
        readonly ProxyWizardModel proxyWizardModel;

        public TentacleSetupWizardModel(string selectedInstance) : this(selectedInstance, ApplicationName.Tentacle, new ProxyWizardModel(selectedInstance, ApplicationName.Tentacle))
        {
        }

        public TentacleSetupWizardModel(string selectedInstance, ApplicationName applicationName, ProxyWizardModel proxyWizardModel)
        {
            this.applicationName = applicationName;
            this.proxyWizardModel = proxyWizardModel;

            InstanceName = selectedInstance;
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            HomeDirectory = Path.Combine(Path.GetPathRoot(programFiles), "Octopus");
            ApplicationInstallDirectory = Path.Combine(Path.GetPathRoot(programFiles), "Octopus\\Applications");
            if (InstanceName != ApplicationInstanceRecord.GetDefaultInstance(applicationName))
            {
                HomeDirectory = Path.Combine(HomeDirectory, InstanceName);
                ApplicationInstallDirectory = Path.Combine(ApplicationInstallDirectory, InstanceName);
            }
            OctopusServerUrl = "http://";
            ListenPort = "10933";
            UseUsernamePasswordAuthMode = true;
            Username = string.Empty;
            ApiKey = string.Empty;
            MachineName = Environment.MachineName;
            OctopusThumbprint = "";
            FirewallException = false;
            Validator = CreateValidator();
            ServerCommsPort = "10943";
            CommunicationStyle = CommunicationStyle.TentaclePassive;
            

            // It would be nice to do this by sniffing for the advfirewall command, but doing
            // so would slow down showing the wizard. This check identifies and excludes Windows Server 2003.
            FirewallExceptionPossible = Environment.OSVersion.Platform != PlatformID.Win32NT ||
                Environment.OSVersion.Version.Major > 5;
        }

        public bool AreTenantsSupported { get; private set; } = false;
        public bool ShowMachinePolicySelection { get; private set; } = false;
        public string InstanceName { get; private set; }
        public bool FirewallException { get; set; }
        public bool FirewallExceptionPossible { get; set; }

        string TentacleExe => string.IsNullOrEmpty(PathToTentacleExe) ? CommandLine.PathToTentacleExe() : PathToTentacleExe;

        public string PathToTentacleExe { get; set; }

        public string HomeDirectory
        {
            get => homeDirectory;
            set
            {
                if (value == homeDirectory) return;
                homeDirectory = value;
                OnPropertyChanged();
            }
        }

        public string ApplicationInstallDirectory
        {
            get => applicationInstallDirectory;
            set
            {
                if (value == applicationInstallDirectory) return;
                applicationInstallDirectory = value;
                OnPropertyChanged();
            }
        }

        public MachineType MachineType
        {
            get => machineType;
            set
            {
                if (value == machineType) return;
                machineType = value;
                OnPropertyChanged("MachineType");
            }
        }

        public CommunicationStyle CommunicationStyle
        {
            get => communicationStyle;
            set
            {
                if (value == communicationStyle) return;
                communicationStyle = value;
                OnPropertyChanged();
                OnPropertyChanged("IsTentacleActive");
                OnPropertyChanged("IsTentaclePassive");
            }
        }

        public bool IsTentacleActive
        {
            get => CommunicationStyle == CommunicationStyle.TentacleActive;
            set
            {
                CommunicationStyle = value ? CommunicationStyle.TentacleActive : CommunicationStyle.TentaclePassive;
                ProxyWizardModel.ShowProxySettings = value;
            }
        }

        public bool IsTentaclePassive
        {
            get => CommunicationStyle == CommunicationStyle.TentaclePassive;
            set
            {
                CommunicationStyle = value ? CommunicationStyle.TentaclePassive : CommunicationStyle.TentacleActive;
                ProxyWizardModel.ShowProxySettings = !value;
            }
        }

        public string OctopusServerUrl
        {
            get => octopusServerUrl;
            set
            {
                if (value == octopusServerUrl) return;
                octopusServerUrl = value;
                OnPropertyChanged();
                HaveCredentialsBeenVerified = false;
            }
        }

        void ClearAuthModeOption()
        {
            useUsernamePasswordAuthMode = false;
            useApiKeyAuthMode = false;
        }

        public bool UseUsernamePasswordAuthMode
        {
            get => useUsernamePasswordAuthMode;
            set
            {
                if (value.Equals(useUsernamePasswordAuthMode)) return;
                ClearAuthModeOption();
                useUsernamePasswordAuthMode = value;
                HaveCredentialsBeenVerified = false;
                OnPropertyChanged();
            }
        }
        
        public bool UseApiKeyAuthMode
        {
            get => useApiKeyAuthMode;
            set
            {
                if (value.Equals(useApiKeyAuthMode)) return;
                ClearAuthModeOption();
                useApiKeyAuthMode = value;
                HaveCredentialsBeenVerified = false;
                OnPropertyChanged();
            }
        }

        public string ApiKey
        {
            get => apiKey;
            set
            {
                if (value == apiKey) return;
                apiKey = value;
                OnPropertyChanged();
                HaveCredentialsBeenVerified = false;
            }
        }

        public string Username
        {
            get => username;
            set
            {
                if (value == username) return;
                username = value;
                OnPropertyChanged();
                HaveCredentialsBeenVerified = false;
            }
        }

        public string Password
        {
            get => password;
            set
            {
                if (value == password) return;
                password = value;
                OnPropertyChanged();
                HaveCredentialsBeenVerified = false;
            }
        }

        public bool HaveCredentialsBeenVerified
        {
            get => haveCredentialsBeenVerified;
            set
            {
                if (value.Equals(haveCredentialsBeenVerified)) return;
                haveCredentialsBeenVerified = value;
                OnPropertyChanged();
            }
        }

        public string[] PotentialEnvironments
        {
            get => potentialEnvironments;
            set
            {
                if (Equals(value, potentialEnvironments)) return;
                potentialEnvironments = value;
                OnPropertyChanged();
            }
        }

        public string[] PotentialTenantTags
        {
            get => potentialTenantTags;
            set
            {
                if (Equals(value, potentialTenantTags)) return;
                potentialTenantTags = value;
                OnPropertyChanged();
            }
        }

        public string[] PotentialTenants
        {
            get => potentialTenants;
            set
            {
                if (Equals(value, potentialTenants)) return;
                potentialTenants = value;
                OnPropertyChanged();
            }
        }

        public string[] PotentialWorkerPools
        {
            get => potentialWorkerPools;
            set
            {
                if (Equals(value, potentialWorkerPools)) return;
                potentialWorkerPools = value;
                OnPropertyChanged();
            }
        }

        public string[] PotentialRoles
        {
            get => potentialRoles;
            set
            {
                if (Equals(value, potentialRoles)) return;
                potentialRoles = value;
                OnPropertyChanged();
            }
        }
        public string[] PotentialMachinePolicies
        {
            get => potentialMachinePolicies;
            set
            {
                if (Equals(value, potentialMachinePolicies)) return;
                potentialMachinePolicies = value;
                OnPropertyChanged();
            }
        }

        public string SelectedEnvironment
        {
            get => selectedEnvironment;
            set
            {
                if (value == selectedEnvironment) return;
                selectedEnvironment = value;
                OnPropertyChanged();
            }
        }

        public string SelectedRoles
        {
            get => selectedRoles;
            set
            {
                if (value == selectedRoles) return;
                selectedRoles = value;
                OnPropertyChanged();
            }
        }

        public string SelectedTenantTags
        {
            get => selectedTenantTags;
            set
            {
                if (value == selectedTenantTags) return;
                selectedTenantTags = value;
                OnPropertyChanged();
            }
        }

        public string SelectedTenants
        {
            get => selectedTenants;
            set
            {
                if (value == selectedTenants) return;
                selectedTenants = value;
                OnPropertyChanged();
            }
        }

        public string SelectedWorkerPool
        {
            get => selectedWorkerPool;
            set
            {
                if (value == selectedWorkerPool) return;
                selectedWorkerPool = value;
                OnPropertyChanged();
            }
        }

        public string SelectedMachinePolicy
        {
            get => selectedMachinePolicy;
            set
            {
                if (value == selectedMachinePolicy) return;
                selectedMachinePolicy = value;
                OnPropertyChanged();
            }
        }

        public string MachineName
        {
            get => machineName;
            set
            {
                if (value == machineName) return;
                machineName = value;
                OnPropertyChanged();
            }
        }

        public string OctopusThumbprint
        {
            get => octopusThumbprint;
            set
            {
                if (value == octopusThumbprint) return;
                octopusThumbprint = (value ?? string.Empty).Trim();
                OnPropertyChanged();
            }
        }

        public string ListenPort
        {
            get => listenPort;
            set
            {
                if (value == listenPort) return;
                listenPort = value;
                OnPropertyChanged();
            }
        }

        public string ServerCommsPort
        {
            get => serverCommsPort;
            set
            {
                if (value == serverCommsPort) return;
                serverCommsPort = value;
                OnPropertyChanged();
            }
        }

        public string ServerWebSocket
        {
            get => serverWebSocket;
            set
            {
                if (value == serverWebSocket) return;
                serverWebSocket = value;
                OnPropertyChanged();
            }
        }

        public OctopusServerConfiguration Handshake
        {
            get => handshake;
            set
            {
                if (Equals(value, handshake)) return;
                handshake = value;
                OnPropertyChanged();
                OnPropertyChanged("HasHandshake");
                OnPropertyChanged("AwaitingHandshake");
            }
        }

        public bool HasHandshake => Handshake != null;

        public bool AwaitingHandshake => Handshake == null;

        public IEnumerable<OctoService> Services
        {
            get { yield return new OctoService(TentacleExe, InstanceName); }
        }

        public ProxyWizardModel ProxyWizardModel => proxyWizardModel;

        public async Task VerifyCredentials(ILog logger)
        {
            try
            {
                OctopusServerEndpoint endpoint = null;
                if (useApiKeyAuthMode == true)
                {
                    endpoint = new OctopusServerEndpoint(OctopusServerUrl, apiKey, credentials: null);
                }
                else
                {
                    endpoint = new OctopusServerEndpoint(OctopusServerUrl);
                }

                if (!ProxyWizardModel.UseNoProxy)
                {
                    var proxy = string.IsNullOrWhiteSpace(ProxyWizardModel.ProxyServerHost)
                        ? WebRequest.GetSystemWebProxy()
                        : new WebProxy(new UriBuilder("http", ProxyWizardModel.ProxyServerHost, ProxyWizardModel.ProxyServerPort).Uri);

                    proxy.Credentials = string.IsNullOrWhiteSpace(ProxyWizardModel.ProxyUsername)
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential(ProxyWizardModel.ProxyUsername, ProxyWizardModel.ProxyPassword);

                    endpoint.Proxy = proxy;
                }

                using (var client = await OctopusAsyncClient.Create(endpoint))
                {
                    var repository = new OctopusAsyncRepository(client);
                    logger.Info("Connecting to server: " + OctopusServerUrl);

                    var root = repository.Client.RootDocument;
                    logger.Info("Connected successfully, Octopus Server version: " + root.Version);

                    if (UseUsernamePasswordAuthMode == true)
                    {
                        logger.Info($"Authenticating as {username}...");
                        await repository.Users.SignIn(new LoginCommand { Username = username, Password = password });
                    }

                    logger.Info("Authenticated successfully");

                    logger.Info("Getting available roles...");
                    PotentialRoles = (await repository.MachineRoles.GetAllRoleNames()).ToArray();
                    logger.Info("Getting available environments...");
                    PotentialEnvironments = (await repository.Environments.GetAll()).Select(e => e.Name).ToArray();

                    var workerPools = await repository.WorkerPools.GetAll();
                    PotentialWorkerPools = workerPools.Select(e => e.Name).ToArray();
                    SelectedWorkerPool = workerPools.FirstOrDefault(wp => wp.IsDefault)?.Name;

                    AreTenantsSupported = repository.Client.RootDocument.HasLink("Tenants");
                    if (AreTenantsSupported)
                    {
                        logger.Info("Getting available tenant tags...");
                        PotentialTenantTags = (await repository.TagSets.GetAll()).SelectMany(tt => tt.Tags.Select(t => t.CanonicalTagName)).ToArray();

                        logger.Info("Getting available tenants...");
                        PotentialTenants = (await repository.Tenants.GetAll()).Select(tt => tt.Name).ToArray();
                    }

                    SelectedEnvironment = PotentialEnvironments.FirstOrDefault();
                    if (SelectedEnvironment == null)
                    {
                        logger.Error("No environments exist. Please use the Octopus web portal to create an environment, then try again.");
                        return;
                    }

                    try
                    {
                        logger.Info("Getting available machine policies...");
                        var machinePolicies = await repository.MachinePolicies.FindAll();
                        var defaultMachinePolicy = machinePolicies.First(x => x.IsDefault);
                        PotentialMachinePolicies = machinePolicies.Select(e => e.Name).ToArray();
                        ShowMachinePolicySelection = PotentialMachinePolicies.Length > 1; // Only show policy selection if they have more than just the default machine policy.

                        SelectedMachinePolicy = PotentialMachinePolicies.FirstOrDefault(x => x == defaultMachinePolicy.Name); // Name is unique, so this is ok.
                        if (SelectedMachinePolicy == null)
                        {
                            logger.Error("No machine policies exist. Please confirm your Octopus web portal contains at least one machine policy, then try again.");
                            return;
                        }
                    }
                    catch
                    {
                        // Don't throw. Make this backwards compatible with pre-3.4 installations.
                        ShowMachinePolicySelection = false;
                        logger.Info("Machine policies do not appear to be available for the given Octopus instance, so we are skipping their selection.");
                    }

                    logger.Info("Credentials verified");
                    HaveCredentialsBeenVerified = true;
                }
            }
            catch (OctopusValidationException ex)
            {
                logger.Error(ex.Message);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        IValidator CreateValidator()
        {
            var validator = new InlineValidator<TentacleSetupWizardModel>();
            validator.RuleSet("TentacleActive", delegate
            {
                validator.RuleFor(m => m.OctopusServerUrl).Must(BeAValidUrl).WithMessage("Please enter a valid Octopus Server URL");
                validator.RuleFor(m => m.ApiKey).NotEmpty().WithMessage("Please enter your API key").When(t => t.UseApiKeyAuthMode == true);
                validator.RuleFor(m => m.ApiKey).Must(s => s.StartsWith("API-")).WithMessage("The API key you provided doesn't start with \"API-\" as expected. It's possible you've copied the wrong thing from the Octopus Portal.").When(t => t.UseApiKeyAuthMode == true);
                validator.RuleFor(m => m.Username).NotEmpty().WithMessage("Please enter your username").When(t => t.UseUsernamePasswordAuthMode == true);
                validator.RuleFor(m => m.Password).NotEmpty().WithMessage("Please enter your password").When(t => t.UseUsernamePasswordAuthMode == true);
            });
            validator.RuleSet("TentaclePassive", delegate
            {
                validator.RuleFor(m => m.ListenPort).Matches("^[0-9]+$").WithMessage("Please enter a TCP port for Tentacle to listen on");
                validator.RuleFor(m => m.OctopusThumbprint).Must(s => !s.StartsWith("API")).WithMessage("This is an API key, not an Octopus Server certificate thumbprint");
                validator.RuleFor(m => m.OctopusThumbprint).Matches("^[A-z0-9]{30,50}$").WithMessage("Please paste your Octopus Server certificate thumbprint");
            });
            validator.RuleSet("TentacleActiveDetails", delegate
            {
                validator.RuleFor(m => m.MachineName).NotEmpty().WithMessage("Please enter a machine name");
                validator.RuleFor(m => m.SelectedRoles).NotEmpty().WithMessage("Please select or enter at least one role").Unless(m => m.MachineType == MachineType.Worker);
                validator.RuleFor(m => m.SelectedEnvironment).NotEmpty().WithMessage("Please select an environment").Unless(m => m.MachineType == MachineType.Worker);
                validator.RuleFor(m => m.SelectedWorkerPool).NotEmpty().WithMessage("Please select a worker pool").Unless(m => m.MachineType == MachineType.DeploymentTarget);
                //validator.RuleFor(m => m.SelectedMachinePolicy).NotEmpty().WithMessage("Please select a machine policy");
            });
            return validator;
        }

        bool BeAValidUrl(string s)
        {
            Uri uri;
            return !string.IsNullOrWhiteSpace(s)
                && Uri.TryCreate(s, UriKind.Absolute, out uri)
                && (uri.Scheme == "http" || uri.Scheme == "https");
        }
        string[] SelectedRolesArray
        {
            get { return (selectedRoles ?? string.Empty).Split(';', ',', ' ').Select(r => r.Trim()).NotNullOrWhiteSpace().ToArray(); }
        }

        string[] SelectedTenantTagsArray
        {
            get { return (selectedTenantTags ?? string.Empty).Split(';', ',').Select(r => r.Trim().Trim('"')).NotNullOrWhiteSpace().ToArray(); }
        }

        string[] SelectedTenantsArray
        {
            get { return (selectedTenants ?? string.Empty).Split(';', ',').Select(r => r.Trim().Trim('"')).NotNullOrWhiteSpace().ToArray(); }
        }


        public IEnumerable<CommandLineInvocation> GenerateScript()
        {
            pathToConfig = Path.Combine(HomeDirectory, ((ApplicationInstanceRecord.GetDefaultInstance(applicationName) != InstanceName) ? "Tentacle-" + InstanceName : InstanceName) + ".config");

            yield return Cli("create-instance").Argument("config", pathToConfig).Build();
            yield return Cli("new-certificate").Flag("if-blank").Build();
            yield return Cli("configure").Flag("reset-trust").Build();

            var config = Cli("configure")
                .Argument("app", applicationInstallDirectory)
                .Argument("port", ListenPort)
                .Argument("noListen", IsTentacleActive.ToString());

            yield return config.Build();

            // TODO: Use octopus server

            if (IsTentacleActive)
            {
                ProxyWizardModel.Executable = TentacleExe;
                foreach (var script in ProxyWizardModel.GenerateScript())
                {
                    yield return script;
                }

                var register = Cli(MachineType==MachineType.Worker ? "register-worker" : "register-with")
                    .Argument("server", OctopusServerUrl)
                    .Argument("name", machineName)
                    .Argument("comms-style", CommunicationStyle.TentacleActive)
                    .Argument("server-comms-port", serverCommsPort)
                    .Flag("force");

                if (!string.IsNullOrWhiteSpace(serverWebSocket))
                    register = register.Argument("server-web-socket", serverWebSocket);

                if (useApiKeyAuthMode == true)
                    register.Argument("apiKey", apiKey);
                else
                    register.Argument("username", username)
                        .Argument("password", password);

                if (MachineType == MachineType.DeploymentTarget)
                {
                    register.Argument("environment", SelectedEnvironment);

                    if (AreTenantsSupported)
                    {
                        foreach (var tag in SelectedTenantTagsArray)
                            register.Argument("tenanttag", tag);

                        foreach (var tenant in SelectedTenantsArray)
                            register.Argument("tenant", tenant);
                    }

                    foreach (var role in SelectedRolesArray)
                        register.Argument("role", role);
                }
                else if(MachineType == MachineType.Worker)
                {
                    register.Argument("workerpool", SelectedWorkerPool);
                }

                register.Argument("policy", SelectedMachinePolicy);

                yield return register.Build();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(OctopusThumbprint))
                    yield return Cli("configure").Argument("trust", OctopusThumbprint).Build();

                if (FirewallException && FirewallExceptionPossible)
                    yield return new CommandLineInvocation("netsh", "advfirewall firewall add rule \"name=Octopus Deploy Tentacle\" dir=in action=allow protocol=TCP localport=" + listenPort, "");
            }

            yield return Cli("service").Flag("install").Flag("stop").Flag("start").Build();
        }

        public IEnumerable<CommandLineInvocation> GenerateRollbackScript()
        {
            yield return Cli("delete-instance").Build();
        }

        CliBuilder Cli(string action)
        {
            return CliBuilder.ForTool(TentacleExe, action, InstanceName);
        }
    }
}
