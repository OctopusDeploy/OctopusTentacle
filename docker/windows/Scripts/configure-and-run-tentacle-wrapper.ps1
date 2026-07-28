& ./configure-tentacle.ps1 -Verbose

# Clear sensitive env variables that should not be inherited by the tentacle process or any forked child processes
Remove-Item Env:\ServerApiKey -ErrorAction SilentlyContinue
Remove-Item Env:\BearerToken -ErrorAction SilentlyContinue
Remove-Item Env:\ServerUsername -ErrorAction SilentlyContinue
Remove-Item Env:\ServerPassword -ErrorAction SilentlyContinue

& ./run-tentacle.ps1 -Verbose