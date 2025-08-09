# .NET Framework 4.7.2 Migration Guide

## Current Status

✅ **Stub.System.Data.SQLite.Core.NetStandard package has been removed**
✅ **System.Data.SQLite.Core supports .NET Framework 4.7.2**
✅ **Build and tests pass with the simplified package structure**

## Package Changes Made

### Removed Package
- `Stub.System.Data.SQLite.Core.NetStandard` v1.0.119
  - This was redundant when using `System.Data.SQLite.Core`
  - Was only needed for older .NET Standard scenarios

### Kept Package
- `System.Data.SQLite.Core` v1.0.119
  - Supports both .NET 8.0 and .NET Framework 4.7.2
  - Contains all necessary SQLite binaries and managed code

## Multi-Targeting Setup

To support both .NET 8.0 and .NET Framework 4.7.2, use the provided `Directory.Build.props.multitarget` file:

### Step 1: Enable Multi-Targeting

```bash
# Backup current configuration
cp Directory.Build.props Directory.Build.props.net8only

# Switch to multi-targeting
cp Directory.Build.props.multitarget Directory.Build.props
```

### Step 2: Update Project Files (if needed)

The multi-targeting configuration will automatically apply to all projects. Key settings:

```xml
<TargetFrameworks>net8.0;net472</TargetFrameworks>
```

### Step 3: Framework-Specific Code

Use conditional compilation for framework-specific code:

```csharp
#if NET472
    // .NET Framework 4.7.2 specific code
    using System.Data.SqlClient;
#elif NET8_0
    // .NET 8.0 specific code
    using Microsoft.Data.SqlClient;
#endif
```

## Compatibility Considerations

### What Works on Both Frameworks

✅ **System.Data.SQLite.Core** - Full compatibility
✅ **Core SQLite functionality** - All database operations
✅ **Retry policy** - Transient error handling
✅ **ETW logging** - Event tracing
✅ **JSON configuration** - Settings management

### Framework-Specific Dependencies

#### .NET Framework 4.7.2 Limitations
- Some NuGet packages may need older versions
- Async patterns might differ slightly
- File I/O APIs have minor differences

#### .NET 8.0 Advantages
- Better performance
- Improved async/await support
- Cross-platform support
- Smaller deployment size

## Testing Strategy

### 1. Verify Current Tests Pass
```bash
dotnet test --framework net8.0
```

### 2. After Enabling Multi-Targeting
```bash
# Test on .NET 8.0
dotnet test --framework net8.0

# Test on .NET Framework 4.7.2
dotnet test --framework net472
```

### 3. Key Test Areas
- Database operations
- Transient error handling
- Configuration loading
- ETW event logging

## Package Version Compatibility

| Package | .NET 8.0 | .NET Framework 4.7.2 |
|---------|----------|---------------------|
| System.Data.SQLite.Core | ✅ 1.0.119 | ✅ 1.0.119 |
| Microsoft.Extensions.Configuration | ✅ 9.0.3 | ⚠️ Use 3.1.x |
| System.Text.Json | ✅ 9.0.7 | ⚠️ Use 6.0.x |
| Newtonsoft.Json | ✅ 13.0.3 | ✅ 13.0.3 |

## Build Configuration

### Development Build
```bash
dotnet build -c Debug
```

### Production Build
```bash
dotnet build -c Release
```

### Framework-Specific Build
```bash
# Build only for .NET Framework 4.7.2
dotnet build -f net472

# Build only for .NET 8.0
dotnet build -f net8.0
```

## Deployment

### .NET Framework 4.7.2 Deployment
- Requires .NET Framework 4.7.2 runtime on target machine
- SQLite.Interop.dll will be in x86/x64 folders
- Deploy all DLLs from `bin\Debug\net472` or `bin\Release\net472`

### .NET 8.0 Deployment
- Can be self-contained or framework-dependent
- Cross-platform support (Windows, Linux, macOS)
- Deploy from `bin\Debug\net8.0` or `bin\Release\net8.0`

## Benefits of This Approach

1. **Simplified Dependencies** - Removed redundant Stub package
2. **Future Ready** - Supports both modern and legacy frameworks
3. **Gradual Migration** - Can transition services incrementally
4. **Maintained Compatibility** - No breaking changes to existing code
5. **Reduced Package Size** - Fewer dependencies to manage

## Next Steps

1. **Test multi-targeting locally** before committing
2. **Update CI/CD pipelines** to build both frameworks
3. **Plan migration timeline** for services
4. **Document framework-specific requirements** for your team

## Troubleshooting

### Issue: Build fails on .NET Framework 4.7.2
**Solution**: Ensure you have .NET Framework 4.7.2 Developer Pack installed

### Issue: SQLite.Interop.dll not found
**Solution**: System.Data.SQLite.Core automatically handles this. Check x86/x64 folders.

### Issue: Package version conflicts
**Solution**: Use framework-specific package versions in conditions:
```xml
<PackageReference Include="Microsoft.Extensions.Configuration" 
                  Version="3.1.32" 
                  Condition="'$(TargetFramework)' == 'net472'" />
<PackageReference Include="Microsoft.Extensions.Configuration" 
                  Version="9.0.3" 
                  Condition="'$(TargetFramework)' == 'net8.0'" />
```