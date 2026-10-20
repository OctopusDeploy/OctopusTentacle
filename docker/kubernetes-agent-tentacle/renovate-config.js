// Scoped Renovate config for the Kubernetes Agent Tentacle container image.
// Structured to mirror the root renovate-config.js, but limited to the
// docker/kubernetes-agent-tentacle area and owned by team-yosemite / team-sierra.
// Runs independently of the root config (which manages .NET/nuget for
// team-executions-foundations).

const preCannedPrNotes = {
  greenMeansGo: [
    'Green means go. Any issues in this PR should be caught as part of our tests and/or builds.',
  ],
}

module.exports = {
  timezone: 'Asia/Jerusalem',
  requireConfig: 'optional',
  onboarding: false,

  // dockerfile   -> golang builder image + any literal FROM tags in the Dockerfiles
  // gomod        -> bootstrapRunner go.mod language version / module deps
  // custom.regex -> the runtime-deps base image tag (see customManagers below).
  // The runtime-deps base image tag is not a literal FROM (it is a build-arg
  // sourced from Build.Pack.cs), so it is handled by the custom manager below.
  enabledManagers: ['dockerfile', 'gomod', 'custom.regex'],

  // Limit Renovate to this feature area. build/Build.Pack.cs is included only
  // so the custom manager can reach the runtime-deps base image tag.
  includePaths: [
    'docker/kubernetes-agent-tentacle/**',
    'build/Build.Pack.cs',
  ],

  // The .NET runtime-deps base image tag lives in a C# constant and is passed
  // to the Dockerfile as the RuntimeDepsTag build-arg, so the built-in docker
  // manager cannot see it. Track the constant directly so base-image CVEs
  // (e.g. gnutls in bookworm-slim) get update PRs.
  customManagers: [
    {
      customType: 'regex',
      managerFilePatterns: ['/^build/Build\\.Pack\\.cs$/'],
      matchStrings: [
        'KubernetesTentacleContainerRuntimeDepsTag\\s*=\\s*"(?<currentValue>[^"]+)"',
      ],
      depNameTemplate: 'mcr.microsoft.com/dotnet/runtime-deps',
      datasourceTemplate: 'docker',
    },
  ],

  // Full list of built-in presets: https://docs.renovatebot.com/presets-default/
  extends: [
    'config:recommended',
    'group:monorepos',
    'group:recommended',
    ':rebaseStalePrs',
    ':automergeRequireAllStatusChecks',
  ],

  // Renovate will create a new issue in the repository.
  // This issue has a "dashboard" where you can get an overview of the status of all updates.
  // https://docs.renovatebot.com/key-concepts/dashboard/
  dependencyDashboard: true,
  dependencyDashboardTitle: 'Kubernetes Agent Tentacle Dependency Dashboard',

  platform: 'github',
  repositories: ['OctopusDeploy/OctopusTentacle'],
  reviewers: ['OctopusDeploy/team-yosemite', 'OctopusDeploy/team-sierra'],
  labels: ['dependencies', 'Tentacle', 'kubernetes-agent'],
  branchPrefix: 'renovate-k8s-agent/',

  // Work around a Renovate bug: these internal status checks emit a relative
  // target_url ("key-concepts/minimum-release-age/"), which GitHub rejects with
  // HTTP 422 and Renovate misreports as "Repository has changed during
  // renovation - aborting", killing the whole run before any PR opens. Disabling
  // the checks stops the failing POST; minimumReleaseAge gating is unaffected.
  // https://docs.renovatebot.com/configuration-options/#statuschecknames
  statusCheckNames: {
    minimumReleaseAge: null,
    mergeConfidence: null,
  },

  // Commit as our own bot account instead of Renovate's Mend-owned default.
  gitAuthor: "team-yosemite-bot <teamyosemitebot@octopus.com>",

  // Limit the amount of PRs created
  prConcurrentLimit: 2,
  prHourlyLimit: 1,

  // If set to false, Renovate will upgrade dependencies to their latest release only. Renovate will not separate major or minor branches.
  // https://docs.renovatebot.com/configuration-options/#separatemajorminor
  separateMajorMinor: false,

  // Pin base/builder images to a digest so Renovate raises a PR every time an
  // image is republished under the same tag - this is what surfaces OS-layer
  // CVE fixes that a floating tag alone would never trigger a PR for. Applies
  // to both the .NET runtime-deps base image (gnutls etc. in bookworm-slim)
  // and the golang:<ver>-alpine builder image used to compile bootstrapRunner.
  pinDigests: true,

  packageRules: [
    // The runtime-deps base image bump touches build/Build.Pack.cs, which is
    // owned by team-executions-foundations, so add them as reviewers on top of
    // the default Kubernetes agent reviewers.
    {
      matchManagers: ['custom.regex'],
      additionalReviewers: ['OctopusDeploy/team-executions-foundations'],
      prBodyNotes: [
        ...preCannedPrNotes.greenMeansGo,
        'Bumps the .NET runtime-deps base image tag in build/Build.Pack.cs.',
      ],
    },
    // We must stay on the .NET 8.0 bookworm-slim line. Allow digest bumps and
    // 8.0 patch tags, but never let Renovate move us to 9.0/10.0 or another
    // Debian release. Moving major .NET versions is a deliberate, separate task.
    {
      matchDepNames: ['mcr.microsoft.com/dotnet/runtime-deps'],
      allowedVersions: '/^8\\.0-bookworm-slim$/',
    },
  ],
}
