ЭКРАНЫ БОТОВ — ПАМЯТКА РАЗРАБОТЧИКАМ

1. Файлы и ответственность

Scripts/NeuroScreenEmotion.cs — компонент времени выполнения на корне бота.
Scripts/Editor/NeuroScreenEmotionEditor.cs — кнопки выбора эмоций в Inspector, включая множественное выделение и Undo.
Scripts/Editor/NeuroScreenTools.cs — назначение материалов префабам и открытой сцене, проверка UV и рендеры.
Scripts/Editor/NeuroScreenTransitionTools.cs — обновление шейдера материалов и проверка анимационных переходов.
Shaders/NeuroScreen.shader — URP-шейдер Neuro/Animated Screen, моргание и яркость лица.

Пути здесь указаны относительно Assets/_Tours/Tour_ENDI_NeuroGuide/Ress/Resource.
Редакторский код должен оставаться в папке Editor: он использует UnityEditor и не входит в сборку игры.
Скрипты перенесены вместе с исходными .meta; GUID сохранены. При следующих переносах также переносите .meta или используйте окно Project в Unity.

2. Ресурсы и схема работы

Characters/Neuro_Guide/Prefabs — 16 префабов с компонентом NeuroScreenEmotion на корне.
Characters/Neuro_Guide/Materials/M_Neuro_ScreenEmotions*.mat — отдельный материал Neuro/Animated Screen для каждого скина.
Characters/Neuro_Guide/Textures/ScreenEmotions — атласы, промпты и документация по оформлению.
ScreenSkinMapping.json в этой папке связывает точное имя бота с atlas, material (экран), bodyMaterial (корпус) и пояснением reason.

Экран — отдельный Renderer Monitor_low, слот материала 0. Поддерживаются MeshRenderer + MeshFilter и SkinnedMeshRenderer обновлённого FBX. Корпус использует свои материалы.
Один атлас содержит 16 выражений в сетке 4×4. Обычная смена идёт через моргание: глаза сжимаются, ячейка переключается в закрытом состоянии, затем глаза открываются. Рот на время закрытия приглушается. Переходы в/из Sleeping и Offline идут через затухание. Во сне яркость по умолчанию 45%, у Offline лицо полностью погашено; тёмный фон скина сохраняется.
Компонент меняет _BaseMap_ST и _ScreenState через MaterialPropertyBlock слота 0. Шейдер сжимает область глаз внутри выбранной ячейки; не смешивает два лица. Текстура не заменяется, геометрия и исходные UV меша не изменяются.
Экземпляры одного скина могут использовать один материал и показывать разные эмоции. Сам материал при выборе эмоции не меняется; у разных скинов разные материалы и текстуры.
Компонент владеет параметрами _BaseMap_ST, _ScreenState и _EyeLayout слота 0. Если другая система меняет MaterialPropertyBlock этого слота, она должна сохранять существующие свойства через GetPropertyBlock; не затирайте блок целиком.

3. Индексы эмоций

Отсчёт слева направо, сверху вниз. Порядок одинаков во всех атласах:

 0 Neutral    — нейтральный    |  1 Happy     — радость
 2 Laugh      — смех          |  3 Wink      — подмигивание
 4 Curious    — любопытство   |  5 Surprised — удивление
 6 Thinking   — размышление   |  7 Confused  — замешательство
 8 Sad        — грусть        |  9 Worried   — тревога
10 Angry      — злость        | 11 Sleepy    — сонливость
12 Sleeping   — сон          | 13 Love      — любовь
14 Excited    — восторг       | 15 Offline   — выключен

Не меняйте порядок enum без согласованной перестановки ячеек всех текстур и миграции сериализованных значений в сценах/префабах.

4. Подключение к игровым событиям

Пример компонента игры:

    using UnityEngine;

    public sealed class BotDialogueFace : MonoBehaviour
    {
        [SerializeField] NeuroScreenEmotion screen;

        public void OnDialogueStarted()
        {
            screen.AutoCycle = false;
            screen.SetEmotion(NeuroScreenEmotion.Emotion.Happy);
        }

        public void OnDialogueFinished()
        {
            screen.SetEmotion(NeuroScreenEmotion.Emotion.Neutral);
        }
    }

В Inspector назначьте screen ссылку на NeuroScreenEmotion нужного бота.
Для получения с корня: bot.GetComponent<NeuroScreenEmotion>().
Сам контроллер не анализирует диалог, ИИ или состояние игры: игровая логика вызывает его методы явно.

Публичный API:
SetEmotion(Emotion value) — выбрать выражение по enum с анимационным переходом.
SetEmotion(int index) — выбрать по индексу; значение ограничивается диапазоном 0–15.
SetEmotionImmediate(Emotion value)/SetEmotionImmediate(int index) — мгновенно установить выражение и отменить текущий переход; использовать при загрузке состояния и в служебных рендерах.
NextEmotion()/PreviousEmotion() — циклически перейти к следующему/предыдущему выражению.
CurrentEmotion — запрошенная целевая эмоция, только чтение.
DisplayedEmotion — ячейка, которая отображается сейчас; во время закрытия глаз ещё может отличаться от цели.
IsTransitioning — выполняется ли переход.
EyeOpenness/Brightness — текущее раскрытие глаз и яркость лица, диапазон 0–1.
ScreenRenderer — ссылка на экран, только чтение.
AutoCycle — включить/выключить демонстрационный перебор в Play.
Configure(Renderer target, Vector2 center, float span) — задать экран и параметры развёртки; применяется редакторским установщиком.
Apply() — отменить переход и мгновенно применить целевую эмоцию.
AtlasTransform — рассчитанный вектор отображаемой ячейки (scaleX, scaleY, offsetX, offsetY), только чтение.
AdvanceTransition(float deltaSeconds) — продвинуть анимацию на заданное число секунд. В Play это делает Update; не вызывайте дополнительно из игровой логики. Метод также используется предпросмотром редактора и детерминированными проверками.

Настройки Transitions в Inspector: Animate Transitions (по умолчанию включено), Blink Close Seconds 0.09, Blink Open Seconds 0.14, Fade Out Seconds 0.4, Fade In Seconds 0.6, Sleeping Brightness 0.45. Обычный переход занимает около 0.23 секунды. Новая команда во время анимации перехватывает переход из текущего визуального состояния; последняя цель побеждает, очереди команд нет. Повтор той же цели не перезапускает анимацию.

Auto Cycle использует Cycle Seconds (по умолчанию 1.5, минимум 0.1) и Time.unscaledTime: продолжает отсчёт при Time.timeScale = 0, если Update выполняется. Перебор ждёт завершения текущего перехода. Сама анимация использует Time.unscaledDeltaTime и также работает при timeScale = 0. В редакторе вне Play автоматического перебора нет. Для управления из игры отключайте Auto Cycle, иначе он заменит выбранную игровым событием эмоцию при следующем шаге.

5. Редактор и жизненный цикл

Выберите одного или нескольких ботов и нажмите кнопку эмоции в Inspector либо измените поле Emotion. ExecuteAlways и редакторский NeuroScreenEmotionPreview позволяют увидеть переход вне Play. При вызове SetEmotion из собственного редакторского инструмента зарегистрируйте предпросмотр через NeuroScreenEmotionPreview.Track(controller) или примените SetEmotionImmediate для статического результата.
OnEnable применяет сохранённую эмоцию; изменение сериализованных полей отмечает обновление для следующего Update.
OnDisable отменяет переход и возвращает параметры экрана к значениям материала. Undo/Redo в Inspector применяет восстановленную эмоцию сразу. Изменения, сделанные во время Play, обычно откатываются Unity после выхода из Play.
Автономного случайного моргания и липсинка нет: моргание выполняется при смене эмоции.

6. UV и требования к новым атласам

Сохраните сетку 4×4, порядок эмоций и положение лиц относительно центров ячеек. Оставляйте тёмные поля вокруг лиц, без подписей и рамок.
Текущие PNG имеют размер 1254×1254. Строгое значение размера компоненту не требуется; важны квадратная текстура и та же сетка.
Импорт: Texture Type Default, sRGB On, Wrap Clamp, Filter Bilinear, Generate Mip Maps Off, Compression None, Non Power of 2 None.
Материал: Neuro/Animated Screen, Emotion Atlas (_BaseMap) — нужный атлас, Tint (_BaseColor) — белый, _ScreenState материала — (1,1,0,0). При старом шейдере без _ScreenState контроллер переключает эмоции мгновенно.

Реальная развёртка Monitor_low (округлённо): U 0.00408–0.99599, V 0.18956–0.82406.
UV Center и UV Span вычисляются установщиком по мешу. Масштаб равномерный по двум осям: 0.25 * 0.99 / uvSpan. Центр выборки смещается в выбранную ячейку. Это сохраняет пропорции развёртки и оставляет запас внутри ячейки.
Для индекса i: столбец i % 4, строка Unity 3 - i / 4 (целочисленное деление). Mirror X и Flip Y меняют направление выборки при адаптации к другой модели.
Простое Tiling (0.25, 0.25) без учёта реальной развёртки здесь применять не нужно.

7. Назначение и проверка

Tools > Neuro Screens > Assign matching skin atlases to all bots
  Проверяет карту и ресурсы, заново привязывает материалы корпуса и экрана всем 16 префабам и объектам открытой сцены с точными именами из карты, включая вложенные. Восстанавливает ссылку Screen Renderer и пересчитывает UV Center/Span. Затем сохраняет сцену и assets. Запускайте на сцене с нужными ботами, вне Play. Текущая версия инструмента рассчитана именно на 16 ботов с точными именами из карты и одним Monitor_low на каждом. После обновления FBX с заменой MeshRenderer на SkinnedMeshRenderer запускайте эту команду повторно: привязка делается по имени Monitor_low, а не по прежнему fileID рендерера. Настройки скелета, Animator и клипы команда не изменяет.
  Перед изменениями создаёт отдельную папку Library/NeuroScreenSkinsBackup/<UTC-время запуска>/ с BeforeRebind.unity и резервными копиями префабов. Library — локальный кэш, резервные копии оттуда не входят в Git.

Tools > Neuro Screens > Verify UV and render all emotions
  Сверяет сохранённые префабы и сцену с картой, проверяет 16 эмоций на каждом боте, попадание UV в выбранные ячейки и независимость выражений. Временно перебирает эмоции для рендера и возвращает прежние значения. Выбирает AquaLaboratory и наводит Scene View на него для осмотра.
  Результаты сохраняет в output/ScreenEmotions/Skins от корня проекта, перезаписывая одноимённые предыдущие отчёты и рендеры.
  Unity_16_Skins_Neutral.png и Unity_16_Skins_Happy.png — сравнение всех скинов с одной эмоцией.
  PF_Neuro_*_16_Emotions.png — все выражения отдельного скина.
  GalleryOrder.txt — порядок ботов в общих галереях.
  verification.txt и installation.txt — отчёты проверки и назначения.

При смене числа ботов, имён, структуры префаба или сетки атласа адаптируйте NeuroScreenTools и NeuroScreenEmotion вместе: текущие проверки и рендерные листы рассчитаны на 16×16 и сетку 4×4.
Tools > Neuro Screens > Enable animated screen materials
  Переводит 16 материалов из карты на Neuro/Animated Screen, сохраняя ссылки на атласы, цвет и UV-преобразование; сохраняет материалы, не сохраняет сцену. Исходные материалы копируются в Library/NeuroTransitionBackup, если таких копий ещё нет.

Tools > Neuro Screens > Verify emotion transitions
  Проверяет 4096 пар (16 скинов × 16 исходных × 16 целевых эмоций), быстрые повторные команды, мгновенную отмену, выключение/включение компонента и отсутствие изменения общих материалов. Сохраняет verification.txt, TransitionStages.png и 100 кадров в output/ScreenEmotions/Transitions. В рендере временно скрывает соседние меши через forceRenderingOff, затем восстанавливает их состояние. Строки TransitionStages.png: Neutral→Happy, Neutral→Sleeping, Happy→Offline, Offline→Happy; слева направо — стадии перехода.

Проверено при внедрении в Unity 6000.2.6f2: 4096 пар переходов, быстрый перехват цели, мгновенная отмена и повторное включение компонента. В Play проверено выполнение через Update при Time.timeScale = 0, включая быстрые Angry→Sleeping→Offline→Love. Отчёт — output/ScreenEmotions/Transitions/runtime.txt. Transitions.gif — собранное из Unity-кадров превью текущей реализации; меню проверки обновляет PNG-кадры, GIF при необходимости пересобирается отдельно.

При смене шейдера проверьте поддержку _BaseMap_ST, _ScreenState и _EyeLayout; при другом слоте материала обновите индекс 0 в контроллере. В _ScreenState.x находится раскрытие глаз, в .y — яркость лица. _EyeLayout содержит нижнюю/верхнюю границы области глаз и центр сжатия в локальных координатах ячейки. Нижняя граница и центр измерены отдельно для каждой эмоции исходного атласа и хранятся в EyeLayouts контроллера: это не даёт маске резать рот. При серьёзной перестановке рисунка внутри атласов адаптируйте таблицу EyeLayouts.
Если после переноса появился Missing Script, сначала проверьте исходный GUID .meta и дубликаты скриптов, а не добавляйте компонент заново.

Анимация и эмоции (тестовая связка)
NeuroAnimationEmotion — StateMachineBehaviour на состояниях Neuro_Animator. При входе в состояние находит NeuroScreenEmotion у бота, отключает AutoCycle и вызывает SetEmotion с сохранением моргания/затухания. Эмоцию можно менять прямо в Inspector состояния Animator. TurningOn/Idle -> Neutral, Curious -> Curious, Thinking -> Thinking, Surprised -> Surprised, Fright -> Worried, Sad -> Sad, No -> Angry, Yes/Flight -> Happy, Delight -> Excited, Laugh -> Laugh, Loving -> Love, TurningOff/Off -> Offline.
Tools > Neuro Screens > Connect animation emotions — идемпотентно назначает реакции общему контроллеру. Применяется ко всем ботам, использующим Neuro_Animator; исходные клипы и переходы не меняет. Резервная копия контроллера: output/AnimationDemo/Neuro_Animator.before-emotions.controller.
Tools > Neuro Screens > Render animation demo frames — создаёт временную копию AntiqueBronze и проигрывает исходный AnimatorController с фиксированным шагом Animator.Update(1/60). Использует контроллер и скорость сценового бота (при отсутствии контроллера — Neuro_Animator). Порядок состояний, exit time и смешивание берутся из контроллера; эмоции вызываются его NeuroAnimationEmotion.OnStateEnter, AdvanceTransition получает тот же шаг 1/60. Прямой SampleAnimation, ручное переключение эмоций на границах клипов и дополнительные паузы не используются. Общий контроллер при записи не изменяется.
Кадрирование рассчитывается предварительным проходом Animator (40 секунд); затем Animator перезапускается с начального состояния. Запись заканчивается через секунду после возвращения в начальное состояние, когда посещены все 15 состояний; защитный предел — 60 секунд. Вывод: output/AnimationDemo/Animator60/Frames (960x960, 60 fps) и capture.txt с проверкой эмоций на входах и числом кадров смешивания. Во время захвата не запускайте Play и дождитесь PASS; скрытые объекты восстанавливаются, временная копия удаляется. Play или перекомпиляция прерывают захват. Видео собирается ffmpeg с -framerate 60 и числом кадров из capture.txt, без аудио. Старый ролик 30 fps сохранён отдельно для сравнения.
Обновление 23.10.2026: ForClip поддерживает числовые суффиксы имён клипов (например, _1); Fear соответствует Worried, FlightStart/FlightEnd — Happy. Текущий вывод нового захвата: output/AnimationDemo/Animator60_20261023. После замены контроллера повторно назначьте реакции состояний через Connect animation emotions: импортированный контроллер может не содержать NeuroAnimationEmotion.
