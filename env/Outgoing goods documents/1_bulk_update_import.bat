@echo off
SetLocal EnableDelayedExpansion
cd..
set /a c=0
for /f "UseBackQ Delims=" %%A IN ("config.txt") do (
  set /a c+=1
  if !c!==2 set "DST=%%A"
  if !c!==4 set "RUNBAT=%%A"
  if !c!==6 set "DATA=%%A"
)

rem изменение исх. товарных
call %RUNBAT%\run_local.bat %RUNBAT%\main.py --bulk-update-outgoing-goods

rem загрузка исх. товарных
chcp 1251
call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.ImportDocumentsFromFolder "%DATA%\“оварный поток\»сход€щие документы"
