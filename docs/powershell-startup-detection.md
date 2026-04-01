# PowerShell Startup Detection

## Background

When `powershell.exe` is invoked to run a script it can occasionally start the OS process but silently stall before executing 
any script content — typically due to Group Policy, antivirus, or other startup hooks that block or hang the PowerShell host. 
Tentacle had no way to distinguish this from a legitimately long-running script, so affected deployments would hang indefinitely 
with no useful error.

The startup detection mechanism lets Tentacle detect and report when PowerShell starts but never executes the script body.

## Opting in

Detection is opt-in. To enable it, include the following marker comment somewhere near the top of your script body (before any code you want to run):

```powershell
# TENTACLE-POWERSHELL-STARTUP-DETECTION
```

When Tentacle bootstraps the script it replaces this marker with generated detection code. Scripts that do not include the marker are completely unaffected.

## What happens at runtime

When the marker is present, Tentacle:

1. Writes a `.octopus_powershell_should_run` file to the script workspace before launching PowerShell.
2. Replaces the marker with generated PowerShell that, at the point it executes:
   - Exclusively creates a `.octopus_powershell_started` sentinel file.
   - Verifies the `.octopus_powershell_should_run` file still exists.
   - Exits with code `-47` if either check fails (meaning the monitor already concluded PowerShell never started).
3. Runs a monitoring task concurrently with script execution. If the sentinel file is not created within the startup timeout the monitor:
   - Creates the sentinel itself (so any late-starting PowerShell process will exit immediately).
   - Deletes the should-run file (so any late-starting process that somehow missed the sentinel will also exit).
   - Returns exit code `-47` (`PowerShellNeverStartedExitCode`) with a "process did not start within…" message written to the task log.

## Startup timeout

The default timeout is **5 minutes**. It can be overridden by setting the environment variable:

```
OCTOPUS_TENTACLE_POWERSHELL_STARTUP_TIMEOUT=<TimeSpan>
```

For example, `00:02:00` for a 2-minute timeout.

## Platform support

Currently scoped to `powershell.exe`.