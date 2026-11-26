# Как открыть models upgrade в Unity

Обновление уже подключено к сцене **DragonlandBattlefield**. Чтобы увидеть героя, Stone Warden, 53 модели окружения и ночное небо, достаточно получить проект вместе с файлами Git LFS и открыть сцену. Для игры используются готовые FBX, материалы и префабы из `Assets`. Исходники Blender в `ArtSource` нужны для редактирования моделей.

## 1. Скачать проект и большие файлы

Если проект уже есть на домашнем ПК, закрой Unity и выполни в PowerShell из корня репозитория:

```powershell
git status --short --branch
git lfs install
git switch main
git pull --ff-only origin main
git lfs pull
git log -1 --oneline --grep="^models upgrade$"
```

Если первая команда показывает твои незавершённые изменения, сохрани их перед обновлением. Последняя команда должна вывести хеш и название `models upgrade`, даже если позже в ветку добавились другие коммиты.

Для первого клонирования выполни из папки, где будет лежать проект:

```powershell
git lfs install
git clone --branch main https://github.com/Entrils/HeroesOfDragonlandReborn.git
cd HeroesOfDragonlandReborn
git lfs pull
```

Дождись успешного завершения LFS. Быстрая проверка — `(Get-Item Assets/Art/Textures/Environment/T_BorderwoodNight.exr).Length` должна показать несколько миллионов байт. Файл порядка 130 байт с первой строкой `version https://git-lfs.github.com/spec/v1` означает, что скачался только указатель. В этом случае проверь ошибку `git lfs pull` и доступ к репозиторию.

## 2. Открыть карту

1. В Unity Hub выбери **Add** и укажи корень с папками `Assets`, `Packages` и `ProjectSettings`. Открой проект в **Unity 6000.6.0f1**. Версия URP **17.6.0** уже указана в проекте. [Версия Editor](../ProjectSettings/ProjectVersion.txt) · [Пакеты](../Packages/manifest.json)
2. Дождись окончания импорта и компиляции. Открой **Window → General → Console** и проверь красные ошибки. В окне Project дважды щёлкни [DragonlandBattlefield.unity](../Assets/Scenes/DragonlandBattlefield.unity).
3. Крепость и лес можно осмотреть сразу в Scene. Для осмотра из игровой камеры нажми **Play**, перейди в Game и запусти матч. Для локальной проверки доступна панель **Developer bots (server controlled)** с кнопками **Fill 3v3** и **Start local match (no server connection)**.

## 3. Что должно быть видно

| Что проверить | Где смотреть и как понять, что подключение сработало |
| --- | --- |
| Герой и экипировка | У игрока видны горжет, асимметричные наплечники и пластины корпуса. Проверь бег, перекат и перенос оружия между рукой и спиной. Цепочка ссылок — [BattlefieldPlayer](../Assets/Game/Data/Battlefield/BattlefieldPlayer.prefab) → [PV_BorderScout](../Assets/Art/Characters/Prefabs/PV_BorderScout.prefab) → [SK_BorderScout](../Assets/Art/Characters/Models/SK_BorderScout.fbx). [Референс и проверка брони](BorderScoutHomeImport.md) |
| Stone Warden | В центральном лагере найди босса с расколотой базальтовой короной и бронзовой цепью. Для быстрой проверки внешности можно открыть [BossKnight.prefab](../Assets/Game/Data/Battlefield/BossKnight.prefab) в Prefab Mode. Материал — [M_StoneWarden](../Assets/Art/Materials/ModelPack/M_StoneWarden.mat). [Референс и атаки](NeutralBoss.md) |
| Крепость, руины и лес | Осмотри кладку и кованые створки у входа, корни и ветви в лесу, переход брусчатки в грунт. Для размещения дополнительных модулей бери готовые префабы с LOD и настроенной коллизией из [Environment/Prefabs](../Assets/Art/Environment/Prefabs). |
| Ночное небо | Из игровой камеры посмотри на восток и немного вверх. Луна находится левее и выше большой красной планеты. Звёзды имеют яркую сердцевину и небольшой ореол даже при выключенном Bloom. [Рендер для сравнения](ArtDirection/BorderwoodApproach.png) |

## 4. Если в редакторе результат другой

- **Старый силуэт героя.** Выбери [SK_BorderScout.fbx](../Assets/Art/Characters/Models/SK_BorderScout.fbx) и нажми **Reimport**. Обработчик импорта обновит четыре производных меша внешности. Если префаб по-прежнему показывает старый вариант, выйди из Play Mode и выполни **Heroes of Dragonland → Art → Install Border Scout Cosmetics**.
- **Пропала модель или текстура.** Проверь загрузку LFS, затем выполни **Reimport** для конкретного FBX или текстуры из `Assets/Art`. Для босса проверь [SK_StoneWarden.fbx](../Assets/Art/Characters/Models/SK_StoneWarden.fbx) и [папку карт Stone Warden](../Assets/Art/Textures/Characters/StoneWarden). Существующие префабы находят ассеты по GUID из парных файлов `.meta`.
- **Серый фон или отсутствуют звёзды.** На открытой сцене Battlefield проверь **Window → Rendering → Lighting → Environment → Skybox Material**. Там должен стоять [AshenBorderwoodSky](../Assets/Art/Materials/Environment/AshenBorderwoodSky.mat). В Inspector этого материала проверь поле **Night sky radiance** — в нём должна быть [T_BorderwoodNight.exr](../Assets/Art/Textures/Environment/T_BorderwoodNight.exr). У Main Camera режим фона должен быть **Skybox**. Если проблема видна только в Scene, проверь отображение Skybox в настройках вида и сравни с Game.
- **Сомнения в LOD, коллизии или слотах материалов окружения.** В Edit Mode открой **Heroes of Dragonland → Art Pipeline** и запусти **Validate Fortress Ruins Kit (M55)**, **Validate Ancient Forest Assets (M56)** и **Validate Shared Biome Set (M57)**. Результаты появятся в Console. Команды Build и Apply предназначены для пересборки наборов и изменения сцены. Для обычного импорта достаточно Validate.

На машине подготовки проверены экспорты Blender и ссылки на ассеты. Проверку Console, анимаций в Play Mode, читаемости ночного боя и частоты кадров выполняй в Unity на игровом ПК.
