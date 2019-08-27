@echo off

set DST=E:\Projects\DirectumRX\bin\Debug\DrxUtil

rem отправка исх. документов
call %DST%\DrxUtil.exe -n katya -p 123 -f Sungero.BulkExchangeSolution.Module.SendDocumentsToCounterparties
