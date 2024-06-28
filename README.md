# Blazor record binding demo

This project is a small demo showcasing an approach to data binding in Blazor using `record` objects and immutable
collections.

## Motivation

Imagine you have a non-trivial data model. It's an object that may have nested objects (including collections, which may
also contain other objects). This isn't an uncommon situation.

You want to make a reusable component to which you can bind your model and have it control the various values of the
model's properties. You want to avoid having to bind each individual property every time you use the component.

Ideally, you could simply use something like `@bind-Value="_model""`. Supporting `@bind-{PARAMETER}` also means the
component *should* properly support letting you define your own `@bind-{PARAMETER}:get` and `@bind-{PARAMETER}:set`
bindings, as well as `@bind-{PARAMETER}:after` if you don't care how the data is bound, but still want to do something
whenever it changes.

## How this demo works

### Data flow

A parent component/page binds a value to a child component. When this component does something that should change this
value, the component notifies the parent that the value should be changed. The parent then updates the value and
re-renders.

That's how `[Parameter]` and `EventCallback<T>` work. A component __should not__ update a `parameter directly by using
its setter. Instead, it should notify the parent component/page using `EventCallback<T>.InvokeAsync(newValue)`.

### Records

In our case, we want to bind a complex object, and be able to deal with collections. However, because of how data
binding works, we can only notify the parent that the object itself has changed.

Enter `record` (specifically: `record class`). Using `record`, we're able to easily create _copies_ of our current
parameter, and specify which properties should be different using the `with` keyword.

Example:

_SomeRecord.cs_
```csharp
public record SomeRecord(string Property);
```

_SomeRecordComponent.cs_
```razor
<input @bind:get="Value.Property" @bind:set="HandlePropertyChangedAsync" />

@code {
    [Parameter]
    public SomeRecord Value { get; set; }
    
    [Parameter]
    public EventCallback<SomeRecord> ValueChanged { get; set; }

    private async Task HandlePropertyChangedAsync(T newValue)
    {
        var nextState = state with { Property = newValue };
        await StateChanged.InvokeAsync(nextState);
    }
}
```

We're passing a whole new `record` object, and we're not updating the original object's properties directly.

### Immutable collections

On top of using `record`s for our models, we also use immutable collections (i.e. `ImmutableList`). They prevent us from
accidentally modifying the original collection. All methods that would mutate a regular collection return a new copy of
an immutable collection.

### References

Since `record`s are init-only by default, they're effectively immutable, _but_ `record class`es are still passed by
reference.

Isn't this a problem?

No, because whenever we mutate a `record`, we'll have to create a copy. In the meantime, untouched `record` objects
stay the same, so any references to them won't cause problems.

The same goes for immutable collections and immutable collections of `record` instances. Whenever we update a collection
we create a copy of it, so if a collection's reference stays the same: we haven't modified it or its children.

### Event bubbling

Child components that deal with nested objects will need to notify their parents whenever a change occurs. Their parents
will need to notify _their_ parent, and so on. Otherwise, the top-level parent has no way of knowing that its model was
changed.

Consider the following code:

```csharp
public record Foo(int Id, string Name);

public record Bar(int Id, string Name, Foo foo);
```

If you have a model of type `Bar` in a component, and you bind it to a component, _that_ component has no way of knowing
whether another component depends on the model. If a child component changes the `Foo` portion of the model, the `Bar`
component needs to be notified, and the `Bar` component needs to notify its parent so that the parent can properly
re-render components that also depend on the model.

This event bubbling looks like this:

_FooComponent.razor_
```razor
<input @bind:get="Foo.Name" @bind:set="HandleNameChangedAsync" />

@code {
    [Parameter]
    public Foo Foo { get; set; }
    
    [Parameter]
    public EventCallback<Foo> FooChanged { get; set; }
    
    // Foo changes bubble up to parent component.
    private async Task HandleNameChangedAsync(string name) =>
        await FooChanged.InvokeAsync(Foo with { Name = name });
}
```

_BarComponent.razor_
```razor
<input @bind:get="Bar.Name" @bind:set="HandleNameChangedAsync" />
<FooComponent @bind-Foo:get="Bar.Foo" @bind-Foo:set="HandleFooChangedAsync" />

@code {
    [Parameter]
    public Bar Bar { get; set; }
    
    [Parameter]
    public EventCallback<Bar> BarChanged { get; set; }
    
    // Bar changes bubble up to parent component.
    private async Task HandleNameChangedAsync(string name) =>
        await BarChanged.InvokeAsync(Bar with { Name = name });
        
    // Convert Foo changes to Bar changes and bubble up to parent component.
    private async Task HandleFooChangedAsync(Foo foo) =>
        await BarChanged.InvokeAsync(Bar with { Foo = foo });
}
```

_Page.razor_
```razor
<BarComponent @bind-Bar="_model" />

@code {
    private Bar _model = new Bar(1, "Bar", new Foo(2, "Foo"));
}
```

### First-class support for two-way data binding

Because this works using the standard two-way data binding syntax (2 `[Parameter]`s, 1 value, 1 `EventCallback`), we're
able to re-use these individual components. We're also able to override how their change event handling works by
defining our own `@bind-{PARAMETER}:get/set` pair.

### Working with copies

Remember, we're working with copies here, so the values that bubble up the component tree aren't the same as the ones
in the component's model. This is a good thing, because it gives us access to the "next" state, while keeping our
current (or "previous") state intact.

This means event handlers can handle logic that needs to compare the previous state to the next one.