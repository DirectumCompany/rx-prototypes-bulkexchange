@echo off

set DST=E:\Projects\DirectumRX\bin\Debug\DrxUtil
set RUNBAT=E:\Projects\DirectumRX\src\Generator

rem ��������� ������� ���������.
chcp 1251
call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.DisableJobs

rem ���������� ��������, �������� ����� ������ ������.

call %RUNBAT%\run_local.bat %RUNBAT%\main.py --bulk-update-send-goods

call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.StartGetMessages
