# Как открыть обновление дома в Unity

В обновление входят главное меню и боевой HUD майлстоуна 83, новые версии волка и медведя и документация по майлстоунам 70–82. Для подключения достаточно получить ветку `main` вместе с файлами Git LFS и открыть существующую сцену Battlefield. Меню создаётся при запуске, а игровые префабы уже ссылаются на обновлённые FBX и материалы.

## 1. Получить проект и графику

Если проект уже есть дома, закрой Unity и выполни команды из корня репозитория в PowerShell. Сначала посмотри вывод `git status`. Если дома есть собственные правки, сохрани их в отдельном коммите или отложи перед обновлением.

```powershell
git status --short --branch
git lfs install
git switch main
git pull --ff-only origin main
git lfs pull
git log -1 --oneline
```

Если клонируешь впервые, выполни из папки, в которой будет лежать проект:

```powershell
git lfs install
git clone --branch main https://github.com/Entrils/HeroesOfDragonlandReborn.git
cd HeroesOfDragonlandReborn
git lfs pull
```

Дождись успешного завершения `git lfs pull` до открытия Editor. Если Git сообщает, что команда `lfs` неизвестна, установи Git LFS и повтори команды. Признак неполной загрузки — FBX или PNG размером около 130 байт с первой строкой `version https://git-lfs.github.com/spec/v1`. В таком случае проверь вывод `git lfs pull` и доступ к репозиторию.

## 2. Открыть меню и войти в матч

1. В Unity Hub выбери корень проекта с папками `Assets`, `Packages` и `ProjectSettings`. Открой его в **Unity 6000.6.0f1**. Версия зафиксирована в [ProjectVersion.txt](../ProjectSettings/ProjectVersion.txt).
2. Дождись импорта и компиляции. В **Window → General → Console** проверь красные ошибки, особенно ошибки C# или импорта USS. В окне Project открой [DragonlandBattlefield.unity](../Assets/Scenes/DragonlandBattlefield.unity) и нажми **Play**.
3. В Game View должны появиться иллюстрация крепости, **Play**, **Training** и кнопки профиля, настроек и выхода. **Play** открывает Unranked, Ranked, Private и Custom; **Escape** возвращает на главный экран. Верхняя панель переключения между меню и HUD удалена из браузерного макета. В игре переходом управляет состояние сессии.
4. Для проверки в одиночку в Editor создай комнату через **Play → Private**. В лобби хоста нажми **Fill empty slots with bots**, затем **Set Ready**. Когда игроки обеих команд получат аватары и кнопка станет доступна, нажми **Start match**.
5. В матче проверь миникарту слева сверху, здоровье обоих королей по центру и ресурсы персонажа слева снизу. Кликни в Game View, верни управление персонажу и удерживай **H** для подробностей. Отпусти **H**, затем проверь **K** (навыки) и **I** (инвентарь).

Если вместо нового оформления видно старое меню, в Play Mode найди объект **Dragonland Main Menu** в Hierarchy. Его создаёт `MultiplayerLobbyPanel`, используя `Panel Settings` дочернего `GameplayHudView`. При отсутствии объекта проверь эту ссылку на `UIDocument` в сцене Battlefield. Если меню появилось без фона или стилей, проверь импорт [TitleValley.png](../Assets/Art/UI/Resources/Dragonland/TitleValley.png) и [FrontEnd.uss](../Assets/Art/UI/Resources/Dragonland/FrontEnd.uss). [Эталон меню](Milestone83Menu.png) и [эталон HUD](Milestone83Hud.png) — изображения браузерного макета для сравнения компоновки.

## 3. Проверить зверей

| Модель | Где уже подключена | Что посмотреть |
| --- | --- | --- |
| [Лесной волк](../ArtSource/Creatures/SK_TimberWolf/README.md) | `LowWolf`, `MediumWolf`, `NetworkWolf` | Прищур, тёмная шерсть и шрам в покое. В движении — лапы при рыси и посадку клыков при оскале. |
| [Бурый медведь](../ArtSource/Creatures/SK_BrownBear/README.md) | `MediumBear`, `HighBear`, `NetworkBear` | Силуэт холки, шерсть на плечах и передние когти. При предупреждающем оскале — движение нижней челюсти и языка. |

В окне Project открой двойным щелчком [LowWolf.prefab](../Assets/Game/Data/Battlefield/LowWolf.prefab) и [MediumBear.prefab](../Assets/Game/Data/Battlefield/MediumBear.prefab). В Prefab Mode найди дочерний `SkinnedMeshRenderer`. У волка в слотах должен стоять [M_TimberWolf](../Assets/Art/Materials/ModelPack/M_TimberWolf.mat), у медведя — [M_BrownBear](../Assets/Art/Materials/ModelPack/M_BrownBear.mat). В Inspector материала проверь карты BaseColor, Normal и Mask, затем оцени обоих зверей в лагере с боевой камеры. У `BossBear` отдельный префаб с отладочной геометрией. Для сравнения медведя используй `MediumBear` или `HighBear`.

Если видишь прежнюю модель или пустую текстуру, проверь загрузку LFS из шага 1. После успешной загрузки в Unity выбери соответствующий FBX в `Assets/Art/Creatures/Models` и текстуры в `Assets/Art/Textures/Characters`, затем **Reimport**. Готовые модели лежат в `Assets`, редактируемые исходники Blender и рендеры — в `ArtSource/Creatures`.

## 4. Проверить Windows-сборку

Выйди из Play Mode. Для сборки нужен модуль Windows Build Support для установленной версии Editor. Выбери **Dragonland → Demo → Build Windows Development**. Для версии проекта 0.2.0 результат записывается в `Builds/Demo-0.2.0/Development/`. Проверь `build-result.txt`, запусти `Dragonland.exe` и повтори переход от меню к матчу. Автоматическая проверка HUD использует сетевую конфигурацию игры и запускается из корня проекта:

```powershell
.\Tests\Standalone\RunGameplayHudRespawn.ps1 -Executable .\Builds\Demo-0.2.0\Development\Dragonland.exe -SpectatorCheck
```

Успешный скрипт выводит `PASS` и путь к логам. Перед переносом проверены модели, веса, текстуры и игровые клипы обратным импортом в Blender. Компиляция в Unity, работающий матч и результат Windows-сборки проверяются дома. Подробные сценарии и перечень изменённых файлов находятся в [майлстоуне 83](Milestone83.md), история предыдущих систем — в [указателе майлстоунов 70–82](Milestones70-82.md).
