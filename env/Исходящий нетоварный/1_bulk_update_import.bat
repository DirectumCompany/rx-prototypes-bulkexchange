@echo off

set DST=E:\Projects\DirectumRX\bin\Debug\DrxUtil
set RUNBAT=E:\Projects\DirectumRX\src\Generator
set DATA=E:\BulkExchangeExamples

rem ��������� ���. ��������
call %RUNBAT%\run_local.bat %RUNBAT%\main.py --bulk-update-outgoing-work

rem �������� ���. ��������
chcp 1251
call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.ImportDocumentsFromFolder "%DATA%\���������� �����\��������� ���������"
