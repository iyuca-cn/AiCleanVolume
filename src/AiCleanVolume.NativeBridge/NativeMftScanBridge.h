#pragma once

#include <stdint.h>

namespace AiCleanVolume {
namespace NativeBridge {

public ref class NativeScanOptions sealed
{
public:
    NativeScanOptions();

    property System::String^ Location;
    property int SortMode;
    property System::Int64 MinSizeBytes;
    property int PerLevelLimit;
};

public ref class NativeNodeInfo sealed
{
public:
    property int NodeId;
    property int ParentNodeId;
    property System::String^ Name;
    property System::String^ Path;
    property System::Int64 LogicalBytes;
    property System::Int64 AllocatedBytes;
    property System::Int64 Bytes;
    property int DirectFileCount;
    property int TotalFileCount;
    property int TotalDirectoryCount;
    property int DirectChildDirectoryCount;
    property bool HasChildren;
};

public ref class NativeChildInfo sealed
{
public:
    property bool IsDirectory;
    property int NodeId;
    property int ParentNodeId;
    property System::String^ Name;
    property System::Int64 LogicalBytes;
    property System::Int64 AllocatedBytes;
    property System::Int64 Bytes;
    property int DirectFileCount;
    property int TotalFileCount;
    property int TotalDirectoryCount;
    property int DirectChildDirectoryCount;
    property bool HasChildren;
};

public ref class NativeChildPage sealed
{
public:
    property array<NativeChildInfo^>^ Items;
    property int TotalCount;
};

public ref class NativeMftScanSession sealed : System::IDisposable
{
public:
    static NativeMftScanSession^ Scan(NativeScanOptions^ options);

    NativeNodeInfo^ GetRootNode();
    NativeNodeInfo^ GetNode(int nodeId);
    NativeChildPage^ GetChildren(int nodeId, int start, int count);

    ~NativeMftScanSession();
    !NativeMftScanSession();

private:
    explicit NativeMftScanSession(System::IntPtr session);

    void ThrowIfDisposed();

    System::IntPtr session_;
};

}
}
