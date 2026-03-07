# Scaffold AI Agents Guidelines

## Project Summary
Scaffold is a highly modularized Unity project designed with strict boundaries and separation of concerns, leveraging MVVM, custom IoC, Roslyn analyzers, and source generators.

## Architecture and Standards
For a comprehensive overview of the project's architecture, layers, and strict coding rules, you should always consult:
- [Architecture.md](file:///c:/Users/user/Documents/Unity/Scaffold/Architecture.md)

## How to Test
Mandatory testing is enforced across all project modules.
- **Unity Test Runner:** Open the Unity Editor and use the Test Runner (`Window > General > Test Runner`) to execute both `EditMode` and `PlayMode` tests.
- **Command Line:** You can also run the test suites headlessly via the Unity command line using the `-runTests` parameter.

## How to Build
- **Application Build:** The main application is built through the standard Unity Editor pipeline (`File > Build Settings`) or via Unity CLI in batch mode.
- **Analyzers and Generators:** The custom Roslyn Analyzers (`Analyzers/`) and Source Generators (`Generators/`) are standard .NET projects. If you modify them, you must build them independently (e.g., using `dotnet build`) to generate the DLLs, which Unity then uses during compilation.

## Documentation
Every module must have its accompanying logic documented. Please refer to or create new documentation here:
- [Doc](file:///c:/Users/user/Documents/Unity/Scaffold/Doc)

## Planning
All current planning documents, task lists, and architectural roadmaps are stored here:
- [Plan](file:///c:/Users/user/Documents/Unity/Scaffold/Plan)
