# ⚠️ Project Deprecated

> **This project has been deprecated and is no longer maintained.**
> 
> Development has moved to **Moongate v2** with a completely rewritten architecture.

## ➡️ Active Project: Moongate v2

The new version features:

- **.NET 10** with NativeAOT compilation
- **Lua scripting** engine (replacing JavaScript)
- **Improved architecture** with better separation of concerns
- **Enhanced performance** and memory efficiency
- **Active development** and community support

### 📦 Repository

- **New Repository**: https://github.com/moongate-community/moongatev2
- **Documentation**: See `README.md` in the moongatev2 repository

---

## 📑 Original README (Archived)

![](./images/moongate_logo.png)

<p align="center">
  <img src="https://img.shields.io/badge/platform-.NET%209-blueviolet" alt=".NET 9">
  <img src="https://img.shields.io/badge/AOT-enabled-green" alt="AOT Enabled">
  <img src="https://img.shields.io/badge/scripting-JavaScript-yellow" alt="JavaScript Scripting">
  <img src="https://img.shields.io/badge/license-GPL--3.0-blue" alt="License GPL-3.0">
  <img src="https://img.shields.io/badge/status-deprecated-red" alt="Development Status">
</p>

```
                                                   __
 /'\_/`\                                          /\ \__
/\      \    ___     ___     ___      __      __  \ \ ,_\    __
\ \ \__\ \  / __`\  / __`\ /' _ `\  /'_ `\  /'__`\ \ \ \/  /'__`\
 \ \ \_/\ \/\ \L\ \/\ \L\ \/\ \/\ \/\ \L\ \/\ \L\.\_\ \ \_/\  __/
  \ \_\\ \_\ \____/\ \____/\ \_\ \_\ \____ \ \__/.\_\\ \__\ \____\
   \/_/ \/_/\/___/  \/___/  \/_/\/_/\/___L\ \/__/\/_/ \/__/\/____/
                                      /\____/
                                      \_/__/

              ++ DEPRECATED - Use Moongate v2 instead ++
  !! New version: https://github.com/moongate-community/moongatev2 !!
```

## Overview

Moongate was a cutting-edge, ultra-high-performance Ultima Online server emulator built with .NET 9 and AOT (Ahead-of-Time) compilation. 

**This project is no longer maintained.** All development efforts have moved to **Moongate v2** which features significant improvements including:

- Migration to .NET 10
- Lua scripting engine (replacing JavaScript/Jint)
- Restructured codebase with improved modularity
- Better performance and lower memory footprint

## Migration Guide

If you were using Moongate v1, here's what changed in v2:

| Feature | Moongate v1 | Moongate v2 |
|---------|-------------|-------------|
| .NET Version | .NET 9 | .NET 10 |
| Scripting | JavaScript (Jint) | Lua (MoonSharp) |
| Architecture | Modular | Enhanced modularity |
| Status | Deprecated | Active Development |

## Quick Start (Moongate v2)

```bash
# Clone the new repository
git clone https://github.com/moongate-community/moongatev2.git
cd moongatev2

# Build and run
dotnet restore
dotnet build
dotnet run --project src/Moongate.Server
```

For full documentation, see the [Moongate v2 README](https://github.com/moongate-community/moongatev2/blob/main/README.md).

## Contributing

**🚀 We're actively looking for developers to join the Moongate project!** Whether you're passionate about UO, .NET development, or building high-performance systems, we'd love to have you on board.

We welcome contributors! Whether you're fixing bugs, adding features, or improving documentation:

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'Add amazing feature

Co-authored-by: Qwen-Coder <qwen-coder@alibabacloud.com>'`)
4. **Push** to the branch (`git push origin feature/amazing-feature`)
5. **Open** a Pull Request

### 🎨 Contribution Guidelines

- Follow the existing code style and conventions
- Add tests for new functionality
- Update documentation as needed
- Ensure all tests pass before submitting

### 👥 Areas Where We Need Help

- **Core UO Protocol Implementation** - Packet handlers, game mechanics
- **Scripting Engine Integration** - Lua API development
- **Performance Optimization** - AOT improvements, memory management
- **Testing & QA** - Comprehensive test coverage
- **Documentation** - Technical docs, tutorials, examples

---

## Original Documentation (Archived)

<details>
<summary>Click to expand original README</summary>

### Key Features (v1)

- **AOT Compilation** - Faster startup, lower memory usage
- **JavaScript-Powered Customization** - Jint engine integration
- **Modern Architecture** - Clean, maintainable code
- **Community-First** - Empowering server owners

### Prerequisites (v1)

- .NET 9 SDK
- Ultima Online Classic Client version 7.x
- 4GB RAM minimum

### Installation (v1)

```bash
git clone https://github.com/moongate-community/moongate.git
cd moongate
dotnet restore
dotnet build
dotnet run --project src/Moongate.Server
```

### JavaScript Scripting (v1)

```javascript
// This scripting system has been replaced by Lua in v2
// See moongatev2 documentation for Lua scripting examples
```

</details>

---

## License

This project is licensed under the **GNU General Public License v3.0** - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- **Ultima Online Community** - For keeping the dream alive after all these years
- **ModernUO, RunUO, UOX3, and ServUO Teams** - For creating countless worlds and keeping UO servers alive
- **.NET Team** - For the amazing AOT capabilities
- **Jint Project** - For the excellent JavaScript engine

---

**⚠️ This repository is archived. Please use [Moongate v2](https://github.com/moongate-community/moongatev2) for active development.**
