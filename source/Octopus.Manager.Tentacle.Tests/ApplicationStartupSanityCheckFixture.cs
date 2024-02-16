using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Autofac;
using NUnit.Framework;
using Octopus.Manager.Tentacle.TentacleConfiguration.TentacleManager;

namespace Octopus.Manager.Tentacle.Tests
{
    [TestFixture]
    public class ApplicationStartupSanityCheckFixture
    {
        [Test]
        public void TentacleManagerModelCanBeResolved()
        {
            // If we can resolve the main view model without errors,
            // then we can safely assume all the necessary components
            // have been correctly registered in the IoC container
            var container = App.ConfigureContainer();
            _ = container.Resolve<TentacleManagerModel>();
        }
        
        [Test]
        [Ignore("Run in local environment only. Programmatically starting WPF app will crash TeamCity host.")]
        public void ApplicationCanStartWithoutCrashing()
        { 
            Exception threadException = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var application = new App();
                    Application.ResourceAssembly = Assembly.GetAssembly(typeof (App));
                    application.InitializeComponent();
                    application.Dispatcher.InvokeAsync(() =>
                    {
                        ExitTentacleManager(application);
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    application.Run();
                }
                catch (Exception ex)
                {
                    threadException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            
            if (threadException is not null) throw threadException;
        }
        
        static async void ExitTentacleManager(Application application)
        {
            var watch = new Stopwatch();
            watch.Start();
            while (application.MainWindow is null || application.MainWindow.Title != App.MainWindowTitle || watch.Elapsed >= TimeSpan.FromSeconds(10))
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            watch.Stop();

            if (application.MainWindow.Title != App.MainWindowTitle)
            {
                throw new ApplicationException("Unable to start Tentacle Manager");
            }
            application.MainWindow.Close();
        }
    }
}