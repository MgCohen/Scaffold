# Coding standards

Apply these rules when writing or reviewing code in the project.

## Comments

- **No comments on methods**, only on classes (and types).
- **Exception:** "todo" and "sample" comments are allowed anywhere (e.g. inline sample comments in a method body explaining placeholder or example behavior).

## One class per file

- **Do not put more than one class per file**, unless:
  - One of the classes is **private** (e.g. a private nested class in the same file), or
  - It is a **generic variation** (e.g. `Foo` and `Foo<T>` in the same file).
