#include "NativeMftScanBridge.h"

#include <string.h>

#include <msclr/marshal.h>
#include <vcclr.h>

#pragma managed(push, off)
#include "mftscan.h"

static const char* NativeErrorMessage(MftscanError errorCode)
{
    return mftscan_error_message(errorCode);
}

static const char* NativeErrorDetail()
{
    return mftscan_error_detail();
}

static MftscanError NativeSessionScan(const MftscanSessionOptions* options, void** session)
{
    MftscanSession* nativeSession = nullptr;
    MftscanError errorCode = mftscan_session_scan(options, &nativeSession);
    *session = nativeSession;
    return errorCode;
}

static void NativeSessionFree(void* session)
{
    mftscan_session_free((MftscanSession*)session);
}

static MftscanError NativeSessionGetRootNode(void* session, MftscanNodeInfo* node)
{
    return mftscan_session_get_root_node((const MftscanSession*)session, node);
}

static MftscanError NativeSessionGetNode(void* session, uint32_t nodeId, MftscanNodeInfo* node)
{
    return mftscan_session_get_node((const MftscanSession*)session, nodeId, node);
}

static MftscanError NativeSessionGetChildren(
    void* session,
    uint32_t nodeId,
    uint32_t start,
    uint32_t count,
    MftscanChildBuffer* children)
{
    return mftscan_session_get_children((const MftscanSession*)session, nodeId, start, count, children);
}

static void NativeChildBufferFree(MftscanChildBuffer* children)
{
    mftscan_child_buffer_free(children);
}
#pragma managed(pop)

using namespace System;
using namespace System::Runtime::InteropServices;
using namespace System::Text;

namespace AiCleanVolume {
namespace NativeBridge {

static String^ Utf8ToManagedString(const char* text)
{
    if (text == nullptr || text[0] == '\0') {
        return String::Empty;
    }

    int length = (int)strlen(text);
    array<Byte>^ bytes = gcnew array<Byte>(length);
    Marshal::Copy(IntPtr((void*)text), bytes, 0, length);
    return Encoding::UTF8->GetString(bytes);
}

static String^ WideToManagedString(const wchar_t* text)
{
    return text == nullptr ? String::Empty : gcnew String(text);
}

static Int64 UInt64ToInt64(uint64_t value)
{
    return value > (uint64_t)Int64::MaxValue ? Int64::MaxValue : (Int64)value;
}

static int UInt32ToInt32(uint32_t value)
{
    return value > (uint32_t)Int32::MaxValue ? Int32::MaxValue : (int)value;
}

static int NativeNodeIdToManaged(uint32_t value)
{
    return value == UINT32_MAX ? -1 : UInt32ToInt32(value);
}

static InvalidOperationException^ CreateNativeException(MftscanError errorCode)
{
    String^ message = Utf8ToManagedString(NativeErrorMessage(errorCode));
    String^ detail = Utf8ToManagedString(NativeErrorDetail());
    if (!String::IsNullOrWhiteSpace(detail)) {
        message = message + "：" + detail;
    }

    return gcnew InvalidOperationException(message);
}

static NativeNodeInfo^ ToManagedNode(const MftscanNodeInfo& source)
{
    NativeNodeInfo^ node = gcnew NativeNodeInfo();
    node->NodeId = NativeNodeIdToManaged(source.node_id);
    node->ParentNodeId = NativeNodeIdToManaged(source.parent_node_id);
    node->Name = WideToManagedString(source.name);
    node->Path = WideToManagedString(source.path);
    node->LogicalBytes = UInt64ToInt64(source.logical_size);
    node->AllocatedBytes = UInt64ToInt64(source.allocated_size);
    node->Bytes = UInt64ToInt64(source.bytes);
    node->DirectFileCount = UInt32ToInt32(source.direct_file_count);
    node->TotalFileCount = UInt32ToInt32(source.total_file_count);
    node->TotalDirectoryCount = UInt32ToInt32(source.total_directory_count);
    node->DirectChildDirectoryCount = UInt32ToInt32(source.direct_child_directory_count);
    node->HasChildren = source.has_children;
    return node;
}

static NativeChildInfo^ ToManagedChild(const MftscanChildInfo& source)
{
    NativeChildInfo^ child = gcnew NativeChildInfo();
    child->IsDirectory = source.kind == MFTSCAN_NODE_DIRECTORY;
    child->NodeId = NativeNodeIdToManaged(source.node_id);
    child->ParentNodeId = NativeNodeIdToManaged(source.parent_node_id);
    child->Name = WideToManagedString(source.name);
    child->LogicalBytes = UInt64ToInt64(source.logical_size);
    child->AllocatedBytes = UInt64ToInt64(source.allocated_size);
    child->Bytes = UInt64ToInt64(source.bytes);
    child->DirectFileCount = UInt32ToInt32(source.direct_file_count);
    child->TotalFileCount = UInt32ToInt32(source.total_file_count);
    child->TotalDirectoryCount = UInt32ToInt32(source.total_directory_count);
    child->DirectChildDirectoryCount = UInt32ToInt32(source.direct_child_directory_count);
    child->HasChildren = source.has_children;
    return child;
}

NativeScanOptions::NativeScanOptions()
{
    SortMode = 1;
    MinSizeBytes = -1;
    PerLevelLimit = -1;
}

NativeMftScanSession::NativeMftScanSession(IntPtr session)
    : session_(session)
{
}

NativeMftScanSession^ NativeMftScanSession::Scan(NativeScanOptions^ options)
{
    if (options == nullptr) {
        throw gcnew ArgumentNullException("options");
    }
    if (String::IsNullOrWhiteSpace(options->Location)) {
        throw gcnew InvalidOperationException("扫描位置不能为空。");
    }

    pin_ptr<const wchar_t> location = PtrToStringChars(options->Location);
    MftscanSessionOptions nativeOptions = { 0 };
    nativeOptions.location = location;
    nativeOptions.sort_mode = options->SortMode == 0 ? MFTSCAN_SORT_LOGICAL : MFTSCAN_SORT_ALLOCATED;
    nativeOptions.min_size = options->MinSizeBytes < 0 ? 0ULL : (uint64_t)options->MinSizeBytes;
    nativeOptions.has_limit = options->PerLevelLimit >= 0;
    nativeOptions.limit = options->PerLevelLimit < 0 ? 0U : (size_t)options->PerLevelLimit;

    void* nativeSession = nullptr;
    MftscanError errorCode = NativeSessionScan(&nativeOptions, &nativeSession);
    if (errorCode != MFTSCAN_OK) {
        throw CreateNativeException(errorCode);
    }

    return gcnew NativeMftScanSession(IntPtr(nativeSession));
}

NativeNodeInfo^ NativeMftScanSession::GetRootNode()
{
    ThrowIfDisposed();

    MftscanNodeInfo nativeNode = { 0 };
    MftscanError errorCode = NativeSessionGetRootNode(session_.ToPointer(), &nativeNode);
    if (errorCode != MFTSCAN_OK) {
        throw CreateNativeException(errorCode);
    }

    return ToManagedNode(nativeNode);
}

NativeNodeInfo^ NativeMftScanSession::GetNode(int nodeId)
{
    ThrowIfDisposed();
    if (nodeId < 0) {
        throw gcnew ArgumentOutOfRangeException("nodeId");
    }

    MftscanNodeInfo nativeNode = { 0 };
    MftscanError errorCode = NativeSessionGetNode(session_.ToPointer(), (uint32_t)nodeId, &nativeNode);
    if (errorCode != MFTSCAN_OK) {
        throw CreateNativeException(errorCode);
    }

    return ToManagedNode(nativeNode);
}

NativeChildPage^ NativeMftScanSession::GetChildren(int nodeId, int start, int count)
{
    ThrowIfDisposed();
    if (nodeId < 0) {
        throw gcnew ArgumentOutOfRangeException("nodeId");
    }
    if (start < 0) {
        throw gcnew ArgumentOutOfRangeException("start");
    }
    if (count < 0) {
        throw gcnew ArgumentOutOfRangeException("count");
    }

    MftscanChildBuffer nativeChildren = { 0 };
    MftscanError errorCode = NativeSessionGetChildren(
        session_.ToPointer(),
        (uint32_t)nodeId,
        (uint32_t)start,
        (uint32_t)count,
        &nativeChildren);
    if (errorCode != MFTSCAN_OK) {
        throw CreateNativeException(errorCode);
    }

    NativeChildPage^ page = gcnew NativeChildPage();
    page->TotalCount = UInt32ToInt32(nativeChildren.total_count);
    page->Items = gcnew array<NativeChildInfo^>(UInt32ToInt32(nativeChildren.count));
    for (uint32_t index = 0; index < nativeChildren.count; ++index) {
        page->Items[index] = ToManagedChild(nativeChildren.items[index]);
    }

    NativeChildBufferFree(&nativeChildren);
    return page;
}

void NativeMftScanSession::ThrowIfDisposed()
{
    if (session_ == IntPtr::Zero) {
        throw gcnew ObjectDisposedException("NativeMftScanSession");
    }
}

NativeMftScanSession::~NativeMftScanSession()
{
    this->!NativeMftScanSession();
    GC::SuppressFinalize(this);
}

NativeMftScanSession::!NativeMftScanSession()
{
    if (session_ != IntPtr::Zero) {
        NativeSessionFree(session_.ToPointer());
        session_ = IntPtr::Zero;
    }
}

}
}
