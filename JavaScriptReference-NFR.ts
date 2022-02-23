class Rule {
    Define(name: string, value) { return new Rule; }
    Match(name: string) { return new Rule; }
    Write() { return new Rule; }
    Read() { return new Rule; }
    If(comparison: Comparison) { return new Rule; }
    Value(value) { return new Rule; }
    EnterValue(value) { return new Rule; }
    And() { return new Rule; }
    Or() { return new Rule; }
    Variable(name: string) { return new Rule; }
    Invoke(fun: string) { return new Rule; }
    End() { return new Rule; }
    AsLeft() { return new Rule; }
    AsRight() { return new Rule; }
    ToRoot() { return new Rule; }
}

enum Comparison {
    Equality,
    Difference,
    GreaterThan,
    LowerThan,
    GreaterThanOrEqual,
    LowerThanOrEqual
}