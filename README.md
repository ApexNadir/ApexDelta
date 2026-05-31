# ApexDelta [![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

## Description
Binary Serialiser that needs ZERO information about your data structures.

It can handle deeply nested complex graphs, polymorphism, structs, classes, primitives.

It will maintain all your references.

NO SCHEMA/SERIALISER FUNCTIONS NEEDED!



Has built in delta generator - it only asks that you use its Sync types so it can track those, but if you don't want deltas, don't worry!



[NonSerialized] attribute makes it ignore a member.



## License
This project is licensed under the [GPL 3.0](https://www.gnu.org/licenses/gpl-3.0) license
