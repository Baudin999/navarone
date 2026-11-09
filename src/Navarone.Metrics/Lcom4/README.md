# Lcom4

Subject: class. Default limit: 1. A finding is raised only above the limit.

Lack of cohesion, LCOM4, as Hitz and Montazeri restated it as a graph; the name comes from Briand, Daly and Wüst 1998. Every method is a node. Two methods are joined when they use the same field or when one calls the other. The score is the number of pieces the graph falls into. More than one piece means more than one job in one class.

Reads `TypeFacts.IsTest`, `TypeFacts.Fields` (and `IsStatic` on each) and `TypeFacts.Methods`, and each method's `FieldUses` and `Calls`. The question lists each piece with its methods and fields, so the agent sees what belongs together. `Pieces` hands the same pieces, by symbol, to the class view of `navarone classes <path> --json`, which colours each member by its piece.

Conventions:

- Constructors are left out: a constructor sets every field and would join everything.
- A static factory, a static method that calls a constructor of its own class, uses the fields that constructor sets, and those of the constructors it chains to. A primary constructor sets its parameters: a record's properties, a class's parameter fields.
- A method declared without a body, such as a `[GeneratedRegex]` partial method, is a method: its callers join through it.
- An auto-property counts as a field. A property with a body counts as methods, one per accessor; a read calls its getter, an assignment its setter, and `+=` or `++` both.
- A partial class is one class; the provider merges its declarations.
- Interfaces and types with no measured method are not measured. A positional record has none.
- Static methods count as methods, as in standard LCOM4. A static field joins nothing, so a shared logger or Regex cannot make unrelated methods look cohesive; only calls join static methods.
- A method that uses no instance field, calls no other method of the class and is called by none is left out: a stub getter or `GetHashCode() => base.GetHashCode()` says nothing about cohesion.
- A value class is measured, and the deterministic classifier calls its problem a false positive: every method other than a constructor is an accessor without control flow and without a call, that reads or writes at most one of the class's own fields, so each piece is one field with its accessors, which is data, not a job. See `Navarone.Classifiers`.
- A test class is not measured. The provider decides what a test class is and sets `TypeFacts.IsTest`; the metric only reads the yes or no.
- A piece is a group of methods. A field no method uses belongs to no piece, so a data class with unused properties does not split.
- Only fields declared in this class join methods; inherited fields and fields of other types do not. A field joins whichever instance it is read on, so `other.a` joins like `this.a`.
- A lambda or local function belongs to the method it is written in. A call to a method outside the class joins nothing.
