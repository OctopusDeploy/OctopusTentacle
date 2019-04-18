@ECHO OFF
REM see http://joshua.poehls.me/powershell-batch-file-wrapper/

SET SCRIPTNAME=%~d0%~p0%~n0.ps1
SET ARGS=%*
IF [%ARGS%] NEQ [] GOTO ESCAPE_ARGS

:POWERSHELL
PowerShell.exe -NoProfile -NonInteractive -NoLogo -ExecutionPolicy Unrestricted -Command "& { $ErrorActionPreference = 'Stop'; & '%SCRIPTNAME%' @args; EXIT $LASTEXITCODE }" %ARGS%
EXIT /B %ERRORLEVEL%

:ESCAPE_ARGS
REM Trim surrounding quotes
FOR /f "useback tokens=*" %%a IN ('%ARGS%') DO SET ARGS=%%~a
SET ARGS=%ARGS:"=\"%
SET ARGS=%ARGS:`=``%
SET ARGS=%ARGS:'=`'%
SET ARGS=%ARGS:$=`$%
SET ARGS=%ARGS:{=`}%
SET ARGS=%ARGS:}=`}%
SET ARGS=%ARGS:(=`(%
SET ARGS=%ARGS:)=`)%
SET ARGS=%ARGS:,=`,%
SET ARGS=%ARGS:^%=%
REM Add surrounding quotes
SET ARGS="%ARGS%"

GOTO POWERSHELL