using System.Reflection;
using MediaRenamer.Core.Pipeline;
using MediaRenamer.Theme;

namespace MediaRenamer.UI.Operations;

public interface IOperationDescriptor
{
    int SortOrder { get; }
    Type TargetOperationType { get; }
    string MenuLabel { get; }
    RenameOperation CreateNew(AppTheme theme);
    void Edit(RenameOperation operation, AppTheme theme);
}

public abstract class OperationDescriptor<TOperation> : IOperationDescriptor
    where TOperation : RenameOperation, new()
{
    public abstract int SortOrder { get; }
    public Type TargetOperationType => typeof(TOperation);
    public abstract string MenuLabel { get; }

    public RenameOperation CreateNew(AppTheme theme)
    {
        var instance = new TOperation();
        Configure(instance, theme);
        return instance;
    }

    public void Edit(RenameOperation operation, AppTheme theme)
    {
        if (operation is TOperation typed)
        {
            Configure(typed, theme);
        }
    }

    protected abstract void Configure(TOperation op, AppTheme theme);
}

public static class OperationEditorRegistry
{
    private static readonly Lazy<IReadOnlyList<IOperationDescriptor>> _descriptors = new(() =>
        Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IOperationDescriptor).IsAssignableFrom(t))
            .Select(t => (IOperationDescriptor)Activator.CreateInstance(t)!)
            .OrderBy(d => d.SortOrder)
            .ToList());

    public static IReadOnlyList<IOperationDescriptor> All => _descriptors.Value;

    public static IOperationDescriptor? FindFor(RenameOperation operation) =>
        All.FirstOrDefault(d => d.TargetOperationType == operation.GetType());
}