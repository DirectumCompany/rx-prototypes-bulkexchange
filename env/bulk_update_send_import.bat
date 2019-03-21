@echo off

set DST=D:\Projects\dirx\bin\Debug\DrxUtil
set DATA=D:\tmp\BulkExchangeExamples

rem подготовка данных для 1й порции стрельбы
call do debug dirx run-generator-bulk-update-send

rem ================== ИСХ. ЭДО =================

rem загрузка исх. товарных
chcp 1251
call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.ImportDocumentsFromFolder "%DATA%\Товарный поток\Исходящие документы"
call %DST%\DrxUtil.exe -n Administrator -p 11111 -f Sungero.BulkExchangeSolution.Module.ImportDocumentsFromFolder "%DATA%\Нетоварный поток\Исходящие документы"

rem подписание исх. главбухом и директором
call %DST%\DrxUtil.exe -n katya -p 123 -f Sungero.BulkExchangeSolution.Module.SignImportedDocuments

rem отправка исх. документов
call %DST%\DrxUtil.exe -n katya -p 123 -f Sungero.BulkExchangeSolution.Module.SendDocumentsToCounterparties
