// Several parts of this config such as the excludeList, preCannedPrNotes and some packageRules have been duplicated from the Octopus Server renovate.config as they also apply to Tentacle
// https://github.com/OctopusDeploy/OctopusDeploy/blob/main/renovate-config.js

const excludeList = [
  'dotnet-sdk', // The dotnet SDK update is a non-trivial piece of work.
  'FluentAssertions', // FluentAssertions 8 and above introduced potential fees for developers
]

const preCannedPrNotes = {
  greenMeansGo: [
    'Green means go. Any issues in this PR should be caught as part of our tests and/or builds.',
  ],
}

module.exports = {
  timezone: 'Australia/Brisbane',
  requireConfig: 'optional',
  onboarding: false,
  ignoreDeps: excludeList,
  enabledManagers: ['nuget'],

  // Full list of built-in presets: https://docs.renovatebot.com/presets-default/
  extends: [
    'config:base',
    'group:monorepos',
    'group:recommended',
    ':rebaseStalePrs',
    ':automergeRequireAllStatusChecks',
  ],

  // Renovate will create a new issue in the repository.
  // This issue has a "dashboard" where you can get an overview of the status of all updates.
  // https://docs.renovatebot.com/key-concepts/dashboard/
  dependencyDashboard: true,
  dependencyDashboardTitle: 'Tentacle Dependency Dashboard',

  platform: 'github',
  repositories: ['OctopusDeploy/OctopusTentacle'],
  reviewers: ['OctopusDeploy/team-server-at-scale'],
  labels: ['dependencies', 'Tentacle'],
  branchPrefix: 'renovate-dotnet/',

  // Limit the amount of PRs created
  prConcurrentLimit: 2,
  prHourlyLimit: 1,

  // If set to false, Renovate will upgrade dependencies to their latest release only. Renovate will not separate major or minor branches.
  // https://docs.renovatebot.com/configuration-options/#separatemajorminor
  separateMajorMinor: false,

  packageRules: [
    {
      // These packages use a custom fork of NuGet which we still rely on.
      groupName: 'NuGet Libraries',
      matchPackageNames: ['NuGet.Packaging'],
      enabled: false,
    },
    // Keep the rest of the packages in alphabetical order (of package name). However, non-greenMeansGo packages should be placed before greenMeansGo packages 
    // Non-greenMeansGo packages
    {
      matchPackageNames: ['Autofac'],
      prBodyNotes: [
        'Autofac is very important to Octopus. We need to be careful upgrading this package.',
        'Review release notes to ensure there are no breaking changes.',
      ],
    },
    // greenMeansGo packages
    {
      matchPackageNames: ['Nsubstitute'],
      prBodyNotes: [
        ...preCannedPrNotes.greenMeansGo,
        'Used extensively throughout tests.',
      ],
    },
    {
      matchPackageNames: ['Polly'],
      prBodyNotes: [
        ...preCannedPrNotes.greenMeansGo,
        'Used extensively in the codebase. Any breaking changes are likely to be surfaced in the test suite.',
      ],
    },
  ],
}
