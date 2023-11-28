const excludeList = [
    "dotnet-sdk", // The dotnet SDK update is a non-trivial piece of work.
];

module.exports = {

    timezone: "Australia/Brisbane",
    requireConfig: "optional",
    onboarding: false,

    ignoreDeps: excludeList,
    enabledManagers: ["nuget"],

    // Full list of built-in presets: https://docs.renovatebot.com/presets-default/
    extends: [
        "config:base",
        "group:monorepos",
        "group:recommended",
		":rebaseStalePrs",
        ":automergeRequireAllStatusChecks",
    ],

    // Renovate will create a new issue in the repository.
    // This issue has a "dashboard" where you can get an overview of the status of all updates.
    // https://docs.renovatebot.com/key-concepts/dashboard/
    dependencyDashboard: true,
    dependencyDashboardTitle: "Tentacle Dependency Dashboard",

    platform: "github",
    repositories: ["OctopusDeploy/OctopusTentacle"],
    reviewers: ["OctopusDeploy/team-server-at-scale"],
    labels: ["dependencies", "Tentacle"],
    branchPrefix: "renovate-dotnet/",

    // Limit the amount of PRs created
    prConcurrentLimit: 2,
    prHourlyLimit: 1,

    // If set to false, Renovate will upgrade dependencies to their latest release only. Renovate will not separate major or minor branches.
    // https://docs.renovatebot.com/configuration-options/#separatemajorminor
    separateMajorMinor: false,
};
