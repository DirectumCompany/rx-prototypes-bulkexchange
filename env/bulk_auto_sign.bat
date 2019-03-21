@echo off

set DST=D:\Projects\dirx\bin\Debug\DrxUtil

:loop

chcp 1251
rem Подписание ИОП
call %DST%\DrxUtil.exe -n katya -p 123 -f Sungero.Exchange.Module.SignAndSendDeliveryConfirmation

call %DST%\DrxUtil.exe -n katya -p 123 -f Sungero.BulkExchangeSolution.Module.SignVerifiedDocuments

goto loop