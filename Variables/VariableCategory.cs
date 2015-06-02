using System;
using System.ComponentModel;

namespace Octopus.Shared.Variables
{
    public enum VariableCategory
    {
        [Description("Deployment-level variables are drawn from the project and release being deployed.")] Deployment,

        [Description("Server-level variables describe the Octopus server on which the deployment is running.")] Server,

        [Description("Output variables are collected during execution of a step and made available to subsequent steps using notation " +
            "such as `Octopus.Action[Website].Output[WEBSVR01].Package.InstallationDirectoryPath` to refer to values base on the action and machine that produced them.")] Output,

        [Description("Action-level variables are available during execution of an action. Indexer notion such as `Octopus.Action[Website].TargetRoles` can be used to refer to values for different actions.")] Action,

        [Description("Step-level variables are available during execution of a step. Indexer notion such as `Octopus.Step[Website].Number` can be used to refer to values for different steps.")] Step,

        [Description("Agent-level variables describe the deployment agent or Tentacle on which the deployment is executing.")] Agent,

        [Description("Undocumented variables not avaiable to the user.")] Hidden
    }
}