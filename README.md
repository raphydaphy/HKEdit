# Hollow Knight Exporter
Converts hollow knight level files into usable Unity scenes

## To-Do

  - [x] Full Scene with NoScriptData error free
  - [x] MonoBehavour with primitive properties (bool, int, etc) error free
  - [ ] MonoBehaviour with Unity types (GameObjects, Components, etc) error free
  - [ ] tk2d components error free
  - [ ] FSM components error free
  - [ ] Playable scenes

## Errors

  - **Recursive Serialization is not supported. You can't dereference a PPtr while loading.**<br />
  This happens three times for every MonoBehaviour if the MonoScript is not added to the preload table.
  - **Assertion failed: illegal LocalPathID in persistentmanager**<br />
  I don't know yet

## Warnings
  - **GameObject references runtime script in scene file. Fixing!**<br />
  This because the generated scene file contains MonoScripts which reference DLL's instead of the script location in the editor.

  - **The Animator Controller you have used is not valid. Animations will not play**<br />
  You can safely ignore this but I'm not sure why it happens.