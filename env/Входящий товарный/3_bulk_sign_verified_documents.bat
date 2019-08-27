@echo off

set DST=E:\Projects\DirectumRX\bin\Debug\DrxUtil

rem подписание
chcp 1251
call %DST%\DrxUtil.exe -n boss -p 123 -f Sungero.BulkExchangeSolution.Module.SignVerifiedDocuments
