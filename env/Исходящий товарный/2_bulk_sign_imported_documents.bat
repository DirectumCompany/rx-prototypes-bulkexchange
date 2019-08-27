@echo off

set DST=E:\Projects\DirectumRX\bin\Debug\DrxUtil

rem подписание исх. главбухом и директором
call %DST%\DrxUtil.exe -n katya -p 123 -f Sungero.BulkExchangeSolution.Module.SignImportedDocuments

