---
paths:
  - "Assets/**"
  - "ProjectSettings/**"
---

Не правь Unity-asset форматы напрямую: `*.unity`, `*.prefab`, `*.asset`, `*.mat`, `*.shadergraph`, `*.controller`, `*.anim`, `*.physicMaterial`, `*.lighting`, и содержимое `ProjectSettings/**`. 
Unity держит эти файлы в памяти открытого редактора; внешняя правка либо перетирается при сохранении из Unity, либо вызывает merge-конфликт. Скрипты `Assets/**/*.cs` править можно — они не Unity-managed ассет.

Изменения сцен/префабов/SO-конфигов делает пользователь в редакторе. 
Если нужно поменять scene-state или SO-значение — попроси пользователя сделать это в Unity. На выполнение этих правок стоит механический deny в `.claude/settings.local.json`.
