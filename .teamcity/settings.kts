import jetbrains.buildServer.configs.kotlin.v2019_2.*
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.commitStatusPublisher
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.DotnetVsTestStep
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.dotnetVsTest
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.nuGetPublish
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.powerShell
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.script
import jetbrains.buildServer.configs.kotlin.v2019_2.failureConditions.BuildFailureOnMetric
import jetbrains.buildServer.configs.kotlin.v2019_2.failureConditions.failOnMetricChange
import jetbrains.buildServer.configs.kotlin.v2019_2.triggers.vcs

/*
The settings script is an entry point for defining a TeamCity
project hierarchy. The script should contain a single call to the
project() function with a Project instance or an init function as
an argument.

VcsRoots, BuildTypes, Templates, and subprojects can be
registered inside the project using the vcsRoot(), buildType(),
template(), and subProject() methods respectively.

To debug settings scripts in command-line, run the

    mvnDebug org.jetbrains.teamcity:teamcity-configs-maven-plugin:generate

command and attach your debugger to the port 8000.

To debug in IntelliJ Idea, open the 'Maven Projects' tool window (View
-> Tool Windows -> Maven Projects), find the generate task node
(Plugins -> teamcity-configs -> teamcity-configs:generate), the
'Debug' option is available in the context menu for the task.
*/

version = "2020.1"

project {

    buildType(TestOnLinux)
    buildType(Publish)
    buildType(Build)
    buildType(TestOnWindows)

    params {
        param("teamcity.vcsTrigger.runBuildInNewEmptyBranch", "true")
    }
    buildTypesOrder = arrayListOf(Build, TestOnLinux, TestOnWindows, Publish)
}

object Build : BuildType({
    name = "Build"

    artifactRules = "build/artifacts"

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        powerShell {
            name = "Build"
            scriptMode = file {
                path = "build.ps1"
            }
            param("jetbrains_powershell_scriptArguments", "-verbosity %Cake.Verbosity%")
        }
    }

    features {
        commitStatusPublisher {
            publisher = github {
                githubUrl = "https://api.github.com"
                authType = password {
                    userName = "bob@octopus.com"
                    password = "credentialsJSON:3ff28a52-158e-4221-ad49-1075f1762644"
                }
            }
            param("github_oauth_provider_id", "PROJECT_EXT_17")
            param("github_oauth_user", "Octobob")
        }
    }

    requirements {
        equals("system.Octopus.AgentType", "Build-VS2019")
    }
})

object Publish : BuildType({
    name = "Publish"
    description = "Pushes the nuget package to the internal repository"

    buildNumberPattern = "${Build.depParamRefs.buildNumber}"

    vcs {
        root(DslContext.settingsRoot)

        cleanCheckout = true
        excludeDefaultBranchChanges = true
        showDependenciesChanges = true
    }

    steps {
        nuGetPublish {
            toolPath = "%teamcity.tool.NuGet.CommandLine.DEFAULT%"
            packages = "Octopus.Shared.${Build.depParamRefs["GitVersion.NuGetVersion"]}.nupkg"
            serverUrl = "https://f.feedz.io/octopus-deploy/dependencies/nuget"
            apiKey = "credentialsJSON:eee0f4e4-8dea-4c0a-b6e4-5cb7f70eca3d"
        }
    }

    triggers {
        vcs {
        }
    }

    features {
        commitStatusPublisher {
            publisher = github {
                githubUrl = "https://api.github.com"
                authType = personalToken {
                    token = "credentialsJSON:e3abf97f-cad5-4d88-9a7a-f588c55c53ed"
                }
            }
        }
    }

    dependencies {
        dependency(Build) {
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }

            artifacts {
                cleanDestination = true
                artifactRules = "Octopus.Shared.*.nupkg"
            }
        }
        snapshot(TestOnWindows) {
            onDependencyFailure = FailureAction.CANCEL
            onDependencyCancel = FailureAction.CANCEL
        }
        snapshot(TestOnLinux) {
            onDependencyFailure = FailureAction.CANCEL
            onDependencyCancel = FailureAction.CANCEL
        }
    }

    requirements {
        equals("system.Octopus.OSPlatform", "Windows")
    }
})

object TestOnWindows : BuildType({
    name = "Test on Windows"

    buildNumberPattern = "${Build.depParamRefs.buildNumber}"

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        dotnetVsTest {
            name = "dotnet vstest"
            assemblies = "build/artifacts/win-x64/Octopus.Shared.Tests.dll"
            version = DotnetVsTestStep.VSTestVersion.CrossPlatform
            logging = DotnetVsTestStep.Verbosity.Detailed
            param("dotNetCoverage.dotCover.home.path", "%teamcity.tool.JetBrains.dotCover.CommandLineTools.DEFAULT%")
            param("platform", "auto")
        }
    }

    failureConditions {
        failOnMetricChange {
            metric = BuildFailureOnMetric.MetricType.TEST_COUNT
            units = BuildFailureOnMetric.MetricUnit.DEFAULT_UNIT
            comparison = BuildFailureOnMetric.MetricComparison.LESS
            compareTo = value()
        }
    }

    features {
        commitStatusPublisher {
            publisher = github {
                githubUrl = "https://api.github.com"
                authType = personalToken {
                    token = "credentialsJSON:70b760a0-25e3-406b-9ed2-d73026115dc1"
                }
            }
        }
    }

    dependencies {
        dependency(Build) {
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }

            artifacts {
                cleanDestination = true
                artifactRules = "publish/win-x64=>build/artifacts/win-x64"
            }
        }
    }

    requirements {
        equals("system.Octopus.Purpose", "Test")
        equals("system.Octopus.OSPlatform", "Windows")
    }
})

object TestOnLinux : BuildType({
    name = "Test on Linux"

    buildNumberPattern = "${Build.depParamRefs.buildNumber}"

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        script {
            name = "sudo dotnet vstest"
            scriptContent = "sudo dotnet vstest build/artifacts/linux-x64/Octopus.Shared.Tests.dll /logger:logger://teamcity /TestAdapterPath:/opt/TeamCity/BuildAgent/plugins/dotnet/tools/vstest15 /logger:console;verbosity=detailed"
        }
    }

    failureConditions {
        failOnMetricChange {
            metric = BuildFailureOnMetric.MetricType.TEST_COUNT
            units = BuildFailureOnMetric.MetricUnit.DEFAULT_UNIT
            comparison = BuildFailureOnMetric.MetricComparison.LESS
            compareTo = value()
        }
    }

    features {
        commitStatusPublisher {
            publisher = github {
                githubUrl = "https://api.github.com"
                authType = personalToken {
                    token = "credentialsJSON:70b760a0-25e3-406b-9ed2-d73026115dc1"
                }
            }
        }
    }

    dependencies {
        dependency(Build) {
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }

            artifacts {
                cleanDestination = true
                artifactRules = "publish/linux-x64=>build/artifacts/linux-x64"
            }
        }
    }

    requirements {
        exists("system.Octopus.Docker")
        equals("system.Octopus.OSPlatform", "Linux")
        equals("system.Octopus.Purpose", "Test")
    }
})
