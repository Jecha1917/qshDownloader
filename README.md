# qshDownloader
Загрузчик котировок московской биржи для привода qscalp(и не только) с web серверов, проверяет на появление новых дней добавляя их архив

загружает архив котировок в указанную директорию в конфиге, проходится по списку указанных серверов, которые можно взять на сайте привода qscalp.
HistoryPath - Директория где хранится архив котировок
UrlQshServers - Список адресов где выложен в публичный доступ архив котировок.
ParseQshUrls - регулярка для получеения относительного пути к файлу на веб сервере в первой группе, и имени файла во второй группе.
ParseDate - регулярка для получения даты со списка дней для загрузки в первой группе дата дня.
HoursInterval - интервал проверки серверов, данные выкладываются раз в сутки, если запустить службу в 6:30 по мск то сервис будет запускаться каждый день в 6:30.

пример секции конфига "appsettings.json":
  "WorkerConfig": {
    "HistoryPath": "C:\\Trading\\History",
    "UrlQshServers": [
      "http://erinrv.qscalp.ru",
      "http://qsh.qscalp.ru/Techcap"
    ],
    "ParseQshUrls": "\"([\\w\\/]*[\\/\\-\\d]+([\\.\\-\\w]+\\.qsh))\"",
    "ParseDate": ">(\\d{4}-\\d{2}-\\d{2})<",
    "HoursInterval": 24
  }


на данный момент 20.09.2021 котировки занимают на диске 121Гб
