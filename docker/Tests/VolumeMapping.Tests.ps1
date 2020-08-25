# Pester 5 doesn't yet support parameterised tests
[string]$script:IPAddress = $env:IPAddress
[string]$script:OctopusUsername = $env:OctopusUsername
[string]$script:OctopusPassword = $env:OctopusPassword
[string]$script:TentacleVersion = $env:TentacleVersion
[string]$script:ProjectName = $env:ProjectName

function script:Write-DeploymentLogs($logs) {
 % { $logs.LogElements } | % { Write-Host $_.MessageText }
 % { $logs.Children } | % { Write-DeploymentLogs $_ }
}

function script:New-OctopusRepository() {
	$octopusURI = "http://$($script:IPAddress):8080"
	Write-Host "Using Octopus server at $octopusURI"

	$endpoint = new-object Octopus.Client.OctopusServerEndpoint $octopusURI
	$repository = new-object Octopus.Client.OctopusRepository $endpoint

	$loginObj = New-Object Octopus.Client.Model.LoginCommand
	$loginObj.Username = $script:OctopusUsername
	$loginObj.Password = $script:OctopusPassword
	$repository.Users.SignIn($loginObj)

	[Octopus.Client.OctopusRepository]$repository
}

Describe 'Volume Mounts' {

	BeforeAll {
		$repository = script:New-OctopusRepository
	}

	Context 'C:\TentacleHome' {

		it 'polling-tentacle should contain logs' {
			Test-Path "./Volumes/polling-tentacle/TentacleHome/Logs/OctopusTentacle.txt" | Should -Be $true
		}
		
		it 'listening-tentacle should contain logs' {
			Test-Path "./Volumes/listening-tentacle/TentacleHome/Logs/OctopusTentacle.txt" | Should -Be $true
		}
	}

	Context 'C:\Applications' {

		function script:Clean {
			$project = $repository.Projects.FindByName("MyFirstProject")
			if ($null -ne $project) {
				$repository.Projects.Delete($project)
			}

			Remove-Item .\Volumes\polling-tentacle\Applications\Development -Recurse -Force
			Remove-Item .\Volumes\listening-tentacle\Applications\Development -Recurse -Force
		}

		BeforeEach {
			script:Clean
		}

		AfterEach {
			script:Clean
		}

		it 'should contain deployed packages' {
			if ([System.Environment]::OSVersion.Version -eq '10.0.14393.0') {
				Write-Warning "This test does not run successfully on windows 2016"
				return
			}
			# Reindex built in library. This ensures that Octopus is aware of the
			# nupkg file sitting in C:\Repository
			$task = New-Object Octopus.Client.Model.TaskResource
			$task.Name = "SynchronizeBuiltInPackageRepositoryIndex"
			$task.Description = "Re-index built-in package repository"
			$task.State = [Octopus.Client.Model.TaskState]::Queued

			$Task1 = $repository.Tasks.Create($task)

			try {
				$repository.Tasks.WaitForCompletion($Task1)
			}
			finally {
				# Write the logs from the reindex task to debug any issues
				$details = $repository.Tasks.GetDetails($Task1)
				$details.ActivityLogs | % { script:Write-DeploymentLogs $_ }
			}

			# Create Project
			$pg = $repository.ProjectGroups.FindAll()[0]
			$lc = $repository.Lifecycles.FindAll()[0]
			$env = $repository.Environments.FindAll()[0]
			$project = $repository.Projects.CreateOrModify("MyFirstProject", $pg, $lc)			
			$pkg = New-Object Octopus.Client.Model.PackageResource
			$pkg.PackageId = "Serilog.Sinks.TextWriter"
			$pkg.FeedId = "feeds-builtin"
			$project.DeploymentProcess.AddOrUpdateStep("Deploy").TargetingRoles("app-server", "web-server").AddOrUpdatePackageAction("DeploySeriLog", $pkg)
			$p = $project.Save()

			# Create Release
			$release = new-object Octopus.Client.Model.ReleaseResource
			$release.Version = "1.0.1"
			$release.ProjectId = $p.Instance.Id
			$selectedPackage = New-Object Octopus.Client.Model.SelectedPackage
			$selectedPackage.ActionName = "DeploySeriLog"
			$selectedPackage.StepName = "DeploySeriLog"
			$selectedPackage.Version = "2.1.0"
			$release.SelectedPackages.Add($selectedPackage)
			$release = $repository.Releases.Create($release, $true)

			# Create Deployment
			$deployment = New-Object Octopus.Client.Model.DeploymentResource
			$deployment.ReleaseId = $release.Id
			$deployment.ProjectId = $release.ProjectId
			$deployment.EnvironmentId = $env.Id
			$deployment = $repository.Deployments.Create($deployment)

			# Wait For Deployment
			$task = $repository.Tasks.Get($deployment.TaskId)

			try {
				$repository.Tasks.WaitForCompletion($task, 4, 3)
			}
			finally {
				# Write the logs from the deployment to debug any issues
				$details = $repository.Tasks.GetDetails($task)
				$details.ActivityLogs | % { Write-DeploymentLogs $_ }
			}

			Test-Path "./Volumes/polling-tentacle/Applications/$($env.Name)/$($pkg.PackageId)" | Should -Be $true
			Test-Path "./Volumes/listening-tentacle/Applications/$($env.Name)/$($pkg.PackageId)" | Should -Be $true
		}
	}
}
