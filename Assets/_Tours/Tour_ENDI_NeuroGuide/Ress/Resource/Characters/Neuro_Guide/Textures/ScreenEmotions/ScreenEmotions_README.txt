# Эмоции экрана

T_Neuro_ScreenEmotions_4x4.png — атлас 4×4. Отсчёт слева направо, сверху вниз, индексы 0–15.

0 Нейтральный; 1 Радость; 2 Смех; 3 Подмигивание.
4 Любопытство; 5 Удивление; 6 Размышление; 7 Замешательство.
8 Грусть; 9 Тревога; 10 Злость; 11 Сонливость.
12 Сон; 13 Любовь; 14 Восторг; 15 Выключен.

Unity: Texture Type Default, Wrap Mode Clamp, Filter Mode Bilinear, Generate Mip Maps Off, Compression None, Non Power of 2 None. Исходный PNG 1254×1254 сохраняется без изменения размера.

Для каждого из 16 ботов предусмотрен отдельный атлас и материал Neuro/Animated Screen. Исходный M_Neuro_ScreenEmotions.mat используется AquaLaboratory. Полная карта назначений — ScreenSkinMapping.json, пояснения — ScreenSkins_README.txt. На корнях ботов используется NeuroScreenEmotion; материал назначается только на Monitor_low.

Переключение: выбрать бота, в Inspector нажать кнопку эмоции или выбрать Emotion. Auto Cycle в Play перебирает эмоции с интервалом Cycle Seconds. Скрипт: bot.GetComponent<NeuroScreenEmotion>().SetEmotion(NeuroScreenEmotion.Emotion.Happy); Также поддерживаются SetEmotion(int), NextEmotion(), PreviousEmotion(), AutoCycle.

Фактические UV Monitor_low (округлённо): U 0.00408–0.99599, V 0.18956–0.82406. Компонент учитывает центр UV и применяет одинаковый масштаб по обеим осям, сохраняя пропорции исходной развёртки. Внутри ячейки оставлен запас 1%. Простая формула Tiling 0.25×0.25 для этого меша не подходит. Mirror X и Flip Y позволяют адаптировать компонент к другим развёрткам.

Эмоция задаётся через MaterialPropertyBlock для слота 0: экземпляры одного скина используют общий материал, выражения независимы. У разных скинов свои материалы и атласы. Контроллер управляет параметром _BaseMap_ST этого слота.

Проверено в Unity 6000.2.6f2: все 16 эмоций на всех 16 ботах, 19 712 UV-выборок без выхода в соседние ячейки; независимость ботов и неизменность общего материала. В Play проверены выбор эмоций и Auto Cycle (Neutral → Laugh за два шага); второй бот не изменился. Рендеры анфас всех эмоций и вид под углом находятся в output/ScreenEmotions от корня проекта.

Повторная проверка: Tools > Neuro Screens > Verify UV and render all emotions. Повторная установка: Tools > Neuro Screens > Assign matching skin atlases to all bots. Новые результаты сохраняются в output/ScreenEmotions/Skins.

Создано встроенным image_gen. Промпт сохранён в ScreenEmotions.prompt.txt.

