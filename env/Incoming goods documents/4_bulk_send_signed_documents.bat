@echo off

SetLocal EnableDelayedExpansion
cd..
set /a c=0
for /f "UseBackQ Delims=" %%A IN ("config.txt") do (
  set /a c+=1
  if !c!==2 set "DST=%%A"
)

rem ����������
chcp 1251
call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.SendSignedDocuments
