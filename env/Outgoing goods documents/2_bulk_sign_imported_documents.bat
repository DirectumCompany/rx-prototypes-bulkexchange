@echo off
SetLocal EnableDelayedExpansion
cd..
set /a c=0
for /f "UseBackQ Delims=" %%A IN ("config.txt") do (
  set /a c+=1
  if !c!==2 set "DST=%%A"
)

rem подписание исх. главбухом и директором
call %DST%\DrxUtil.exe -n katya -p 123 -f Sungero.BulkExchangeSolution.Module.SignImportedDocuments

