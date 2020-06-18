# rx-prototypes-bulkexchange
Репозиторий с прототипом массового МКДО.

## Описание
Прототип позволяет выполнить массовые действия с первичными учетными документами:
* загрузка из сервиса обмена;
* верификация входящих документов;
* подписание документов;
* отправка ответов по документам.

### Прототип реализует следующие комплекты документов:
#### Входящий товарный документооборот
* Счет-фактура и документ об отгрузке товаров (выполнении работ) (УПД СЧФДОП)
* Документ об отгрузке товаров (выполнении работ) + Счет-фактура полученный (УПД ДОП + УПД СЧФ)
* Товарная накладная + Счет-фактура полученный (ДПТ + УПД СЧФ)

Контрагенты направляют через сервис обмена в нашу организацию комплекты из первичных учетных документов, относящиеся к товарному документообороту. Вся работа идет в фоновом режиме, без участия пользователей. 
![Вх товарный документооборот](https://user-images.githubusercontent.com/2620711/84864711-1dc96480-b088-11ea-8385-f8ff7941c34c.png)
1. Проверка комплекта документа.  <br>**Точка входа:** серверная функция [AddRelations](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/src/Packages/Sungero.BulkExchangeSolution/Sungero.BulkExchangeSolution.Server/Sungero.Exchange/ModuleServerFunctions.cs#L176-L187) модуля Exchange.
2. Имитация сверки с учетной системой. <br>**Точка входа:** фоновый процесс [VerifyDocuments](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/src/Packages/Sungero.BulkExchangeSolution/Sungero.BulkExchangeSolution.Server/Sungero.Exchange/ModuleJobs.cs#L57-L75) модуля Exchange.
3. Подписание документов. <br>**Точка входа:** клиентский метод [SignVerifiedDocuments](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/src/Packages/Sungero.BulkExchangeSolution/Sungero.BulkExchangeSolution.ClientBase/ModuleClientFunctions.cs#L400-L433) решения BulkExchangeSolution.
4. Отправка ответов по документам. <br>**Точка входа:** фоновый процесс [SendSignedDocuments](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/src/Packages/Sungero.BulkExchangeSolution/Sungero.BulkExchangeSolution.Server/Sungero.Exchange/ModuleJobs.cs#L18-L54) модуля Exchange.
5. Автоматический отказ для некорректного комплекта документов. <br>**Точка входа:** клиентский метод [SendRejectedDocuments](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/src/Packages/Sungero.BulkExchangeSolution/Sungero.BulkExchangeSolution.ClientBase/ModuleClientFunctions.cs#L435-L469) решения BulkExchangeSolution.
 
#### Входящий нетоварный документооборот
*	Счет-фактура и документ об отгрузке товаров (выполнении работ) (УПД СЧФДОП)
*	Счет-фактура полученный + Документ об отгрузке товаров (выполнении работ) (УПД СЧФ + УПД ДОП)
*	Счет-фактура полученный + Документ об отгрузке товаров (выполнении работ) (УПД СЧФ + ДПРР)
*	Счет-фактура полученный + Акт выполненных работ (УПД СЧФ + Неформализованный акт)

Контрагенты направляют через сервис обмена в нашу организацию комплекты из первичных учетных документов, относящиеся к нетоварному документообороту: акты выполненных работ и счета-фактуры.
![Вх нетоварный документооборот](https://user-images.githubusercontent.com/2620711/84865392-2ff7d280-b089-11ea-9ea3-d073138e8802.png)
1. Проверка комплекта документа.  <br>**Точка входа:** серверный метод [AddRelations](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/src/Packages/Sungero.BulkExchangeSolution/Sungero.BulkExchangeSolution.Server/Sungero.Exchange/ModuleServerFunctions.cs#L176-L187) модуля Exchange.
2. Связывание с договором.  <br>**Точка входа:** серверный метод [ProcessDocumenSetFromNewIncomingMessage](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/src/Packages/Sungero.BulkExchangeSolution/Sungero.BulkExchangeSolution.Server/Sungero.Exchange/ModuleServerFunctions.cs#L448-L454) модуля Exchange.
3. Отправка комплекта документов на согласование по регламенту.  <br>**Точка входа:** серверный метод [SendContractStatementForApproval](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/src/Packages/Sungero.BulkExchangeSolution/Sungero.BulkExchangeSolution.Server/Sungero.Exchange/ModuleServerFunctions.cs#L566-L578) модуля Exchange.
4. Отправка ответов по документам. <br>**Точка входа:** фоновый процесс [SendSignedDocuments](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/src/Packages/Sungero.BulkExchangeSolution/Sungero.BulkExchangeSolution.Server/Sungero.Exchange/ModuleJobs.cs#L18-L54) модуля Exchange.



#### Исходящий товарный документооборот
* Счет-фактура и документ об отгрузке товаров (выполнении работ) (УПД СЧФДОП)
* Документ об отгрузке товаров (выполнении работ) + Счет-фактура выставленный	(УПД ДОП + УПД СЧФ)

Процесс подписания и отправки контрагенту через сервис обмена происходит в автоматическом режиме без участия пользователей.
![Исх товарный документооборот](https://user-images.githubusercontent.com/2620711/84867424-320f6080-b08c-11ea-8e6c-f474cdc688a2.png)
Для автоматизации работы с прототипом можно использовать [bat-файлы](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/tree/master/env/Outgoing%20goods%20documents)

1. Автоматическое подписание.  <br>**Точка входа:** клиентский метод [SignImportedDocuments](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/src/Packages/Sungero.BulkExchangeSolution/Sungero.BulkExchangeSolution.ClientBase/ModuleClientFunctions.cs#L89-L111) модуля Exchange.
2. Отправка ответов по документам. <br>**Точка входа:** фоновый процесс [SendSignedDocuments](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/src/Packages/Sungero.BulkExchangeSolution/Sungero.BulkExchangeSolution.Server/Sungero.Exchange/ModuleJobs.cs#L18-L54) модуля Exchange.

## Порядок установки
1. Для работы требуется установленный Directum RX соответствующей версии и генератор.
2. Склонировать репозиторий с rx-prototypes-bulkexchange в папку.
3. Указать в _ConfigSettings.xml DDS:
```xml
<block name="REPOSITORIES">
  <repository folderName="Base" solutionType="Base" url="" />
  <repository folderName="RX" solutionType="Base" url="<адрес локального репозитория>" />
  <repository folderName="<Папка из п.2>" solutionType="Work" 
     url="https://github.com/DirectumCompany/rx-prototypes-bulkexchange" />
</block>
```
4. [Настроить обмен с контрагентами](https://club.directum.ru/webhelp/directumrx/desktop/index.html?sungero_parties_counterparty_card_exchangeboxes.htm).
5. [Настроить автоматический режим](https://club.directum.ru/webhelp/directumrx/desktop/index.html?admin_avtomaticheskii_rezhim.htm) для подтверждения получения документов из сервиса обмена.
6. Настроить запуск сценария массовой работы через сервис обмена. Для этого создать файл `.\Generator\config.py` по аналогии с `.\Generator\config.py.example`. В файле указать:
   -	Данные сервиса обмена Synerdocs – адрес сервиса, код сервиса;
    -	Данные контрагента – ИНН, отпечаток сертификата из сервиса обмена;
    -	Данные НОР – ИНН, отпечаток сертификата из сервиса обмена;
    -	Путь для сохранения сгенерированных документов.
7. Настроить запуск сценариев по потокам. Указать в файле [.\env\config.txt](https://github.com/DirectumCompany/rx-prototypes-bulkexchange/blob/master/env/config.txt) путь к утилите DrxUtil, путь до файла `run_local.bat`, путь для сохранения обновленных документов.
