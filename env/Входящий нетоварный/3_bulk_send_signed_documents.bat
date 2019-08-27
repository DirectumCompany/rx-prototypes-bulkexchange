@echo off

set DST=E:\Projects\DirectumRX\bin\Debug\DrxUtil

rem подписание
chcp 1251
call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.SendSignedDocuments
