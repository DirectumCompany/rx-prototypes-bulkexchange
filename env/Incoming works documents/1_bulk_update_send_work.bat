@echo off

SetLocal EnableDelayedExpansion
cd..
set /a c=0
for /f "UseBackQ Delims=" %%A IN ("config.txt") do (
  set /a c+=1
  if !c!==2 set "DST=%%A"
  if !c!==4 set "RUNBAT=%%A"
)

rem Остановка фоновых процессов.
chcp 1251
call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.DisableJobs

rem Обновление примеров, отправка через сервис обмена.

call %RUNBAT%\run_local.bat %RUNBAT%\main.py --bulk-update-send-work

call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.StartGetMessages
