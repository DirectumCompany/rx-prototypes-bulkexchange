@echo off

set DST=D:\Projects\dirx\bin\Debug\DrxUtil
set DATA=D:\tmp\BulkExchangeExamples

rem ���������� ������ ��� 1� ������ ��������
call do debug dirx run-generator-bulk-update-send

rem ================== ���. ��� =================

rem �������� ���. ��������
chcp 1251
call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.ImportDocumentsFromFolder "%DATA%\�������� �����\��������� ���������"
call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.ImportDocumentsFromFolder "%DATA%\���������� �����\��������� ���������"

rem ���������� ���. ��������� � ����������
call %DST%\DrxUtil.exe -n katya -p 123 -f Sungero.BulkExchangeSolution.Module.SignImportedDocuments

rem �������� ���. ����������
call %DST%\DrxUtil.exe -n katya -p 123 -f Sungero.BulkExchangeSolution.Module.SendDocumentsToCounterparties
