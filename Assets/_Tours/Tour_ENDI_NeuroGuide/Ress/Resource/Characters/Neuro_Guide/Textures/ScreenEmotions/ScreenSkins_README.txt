Атласы экранов под 16 скинов

Назначения задаёт ScreenSkinMapping.json; Unity Tools > Neuro Screens > Assign matching skin atlases to all bots применяет их к префабам и открытой сцене.
У каждого бота отдельная текстура и материал Neuro/Animated Screen. Контроллер NeuroScreenEmotion и нумерация 16 эмоций сохраняются.
Четыре исходных атласа использованы для AquaLaboratory, AntiqueBronze, Retro и Scifi; остальные 12 созданы встроенным image_gen по референсу исходной сетки. Промпты лежат рядом в ScreenEmotions_*.prompt.txt.

1. PF_Neuro_AntiqueBronze
   T_Neuro_ScreenEmotions_Antique_4x4.png
   Золотистая мозаика и бронзовая патина под кремово-бронзовый античный корпус.

2. PF_Neuro_AquaLaboratory
   T_Neuro_ScreenEmotions_4x4.png
   Исходный бирюзовый свет повторяет водные узоры и холодную палитру корпуса.

3. PF_Neuro_BureauOfMeanings
   T_Neuro_ScreenEmotions_BureauOfMeanings_4x4.png
   Кремовые каллиграфические штрихи с бордовым контуром под кремовый корпус и винный орнамент.

4. PF_Neuro_ChemicalStation
   T_Neuro_ScreenEmotions_ChemicalStation_4x4.png
   Зелёный лабораторный свет поддерживает зелёные полосы и колбу-маячок.

5. PF_Neuro_ElectroplantingShop
   T_Neuro_ScreenEmotions_ElectroplantingShop_4x4.png
   Лимонно-жёлтые промышленные сегменты под предупреждающие полосы и серый металл.

6. PF_Neuro_FieldPost
   T_Neuro_ScreenEmotions_FieldPost_4x4.png
   Бумажно-белый свет с синим контуром под почтовый сине-серый корпус.

7. PF_Neuro_KeeperOfMemory
   T_Neuro_ScreenEmotions_KeeperOfMemory_4x4.png
   Фарфорово-кремовый свет с рубиновым акцентом под бело-красную эмаль.

8. PF_Neuro_LadogaGuide
   T_Neuro_ScreenEmotions_LadogaGuide_4x4.png
   Ледяной бело-голубой рисунок под морозный светлый скин.

9. PF_Neuro_MartianScout
   T_Neuro_ScreenEmotions_MartianScout_4x4.png
   Кораллово-оранжевые сегменты под ржаво-красные детали марсианского разведчика.

10. PF_Neuro_MasterOfLogistics
   T_Neuro_ScreenEmotions_MasterOfLogistics_4x4.png
   Охристые модульные сегменты под тёмный оливковый корпус и жёлтый маяк.

11. PF_Neuro_PrismAndOptics
   T_Neuro_ScreenEmotions_PrismAndOptics_4x4.png
   Спектральные штрихи повторяют радужную оптику корпуса.

12. PF_Neuro_RadioConsuctor
   T_Neuro_ScreenEmotions_RadioConsuctor_4x4.png
   Медно-медовые контуры под тёмный деревянно-латунный радиоскин.

13. PF_Neuro_RenaissaneMechanic
   T_Neuro_ScreenEmotions_RenaissaneMechanic_4x4.png
   Пергаментно-медная гравировка под ренессансный корпус с чертёжным орнаментом.

14. PF_Neuro_Retro
   T_Neuro_ScreenEmotions_Retro_4x4.png
   Янтарный точечный ЭЛТ под тёмный корпус с тёплой подсветкой.

15. PF_Neuro_Scifi
   T_Neuro_ScreenEmotions_SciFi_4x4.png
   Бело-голубые сегментированные голограммы под светлый технологичный корпус.

16. PF_Neuro_VenusProbe
   T_Neuro_ScreenEmotions_VenusProbe_4x4.png
   Шампань и белое золото под золотисто-кремовый корпус венерианского зонда.

Проверка и рендеры: Tools > Neuro Screens > Verify UV and render all emotions.
Результаты: output/ScreenEmotions/Skins от корня проекта. В галереях порядок ботов алфавитный, слева направо, сверху вниз (GalleryOrder.txt). Для каждого бота сохраняется лист из всех 16 эмоций.
Настройки текстур: Clamp, Bilinear, без mipmaps и сжатия, NPOT None. UV-преобразование рассчитано по Monitor_low.

Назначено и проверено в Unity 6000.2.6f2: 16 префабов, 16 экземпляров сцены, 16 разных текстур. Все 256 выборов эмоций и 19 712 UV-выборок прошли проверку; независимость выражений сохранена. В Play также проверены сохранённые назначения всех 16 атласов и все 256 переключений. Рендеры включают две общие галереи (Neutral и Happy) и 16 листов всех эмоций по скинам. Сравнение с резервными копиями: в префабах изменились только назначения материалов Monitor_low.

