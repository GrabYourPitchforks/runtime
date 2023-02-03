// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <Windows.h>
#include <heapapi.h>
#include <intsafe.h>
#include "zutil.h"

// This is the special heap we'll allocate from.
HANDLE s_allocHeap = NULL;

BOOL WINAPI DllMain(
    _In_ HINSTANCE hinstDLL,
    _In_ DWORD     fdwReason,
    _In_ LPVOID    lpvReserved
)
{
    BOOL retVal = TRUE;

    switch (fdwReason)
    {
        case DLL_PROCESS_ATTACH:
            // Attempt to create a new heap. If we can't, fall back to the standard process heap.
            s_allocHeap = HeapCreate(0, 0, 0);
            if (s_allocHeap != NULL)
            {
                // Attempt to set the LFH flag on our new heap. Since it's just an optimization, swallow failures.
                // Ref: https://learn.microsoft.com/windows/win32/api/heapapi/nf-heapapi-heapsetinformation
                ULONG ulHeapInformation = 2; // LFH
                HeapSetInformation(s_allocHeap, HeapCompatibilityInformation, &ulHeapInformation, sizeof(ulHeapInformation));
            }
            else
            {
                s_allocHeap = GetProcessHeap();
            }
            break;

        case DLL_PROCESS_DETACH:
            if (s_allocHeap != NULL && s_allocHeap != GetProcessHeap())
            {
                retVal = HeapDestroy(s_allocHeap);
                s_allocHeap = NULL;
            }
            break;

        default:
            break; // We don't care about thread notifications.
    }

    return retVal;
}

typedef struct _DOTNET_ALLOC_COOKIE
{
    PVOID CookieValue;
    union _Size
    {
        SIZE_T RawValue;
        LPVOID EncodedValue;
    } Size;
} DOTNET_ALLOC_COOKIE;

// Historically, the Windows memory allocator always returns addresses aligned to some
// particular boundary. We'll make that same guarantee here just in case somebody
// depends on it.
const SIZE_T DOTNET_ALLOC_HEADER_COOKIE_SIZE_WITH_PADDING = (sizeof(DOTNET_ALLOC_COOKIE) + MEMORY_ALLOCATION_ALIGNMENT - 1) & ~((SIZE_T)MEMORY_ALLOCATION_ALIGNMENT  - 1);
const SIZE_T DOTNET_ALLOC_TRAILER_COOKIE_SIZE = sizeof(DOTNET_ALLOC_COOKIE);

voidpf ZLIB_INTERNAL zcalloc (opaque, items, size)
    voidpf opaque;
    unsigned items;
    unsigned size;
{
    (void)opaque;

    // If initializing a fixed-size structure, zero the memory.
    DWORD dwFlags = (items == 1) ? HEAP_ZERO_MEMORY : 0;

    SIZE_T cbRequested;
    if (sizeof(items) + sizeof(size) <= sizeof(cbRequested))
    {
        // multiplication can't overflow; no need for safeint
        cbRequested = (SIZE_T)items * (SIZE_T)size;
    }
    else
    {
        // multiplication can overflow; go through safeint
        if (FAILED(SizeTMult(items, size, &cbRequested))) { return NULL; }
    }

    // Make sure the actual allocation has enough room for our frontside & backside cookies.
    SIZE_T cbActualAllocationSize;
    if (FAILED(SizeTAdd(cbRequested, DOTNET_ALLOC_HEADER_COOKIE_SIZE_WITH_PADDING + DOTNET_ALLOC_TRAILER_COOKIE_SIZE, &cbActualAllocationSize))) { return NULL; }

    LPVOID pAlloced = HeapAlloc(s_allocHeap, dwFlags, cbActualAllocationSize);
    if (pAlloced == NULL) { return NULL; } // OOM

    // Now set the header & trailer cookies
    DOTNET_ALLOC_COOKIE* pHeaderCookie = (DOTNET_ALLOC_COOKIE*)pAlloced;
    pHeaderCookie->CookieValue = EncodePointer(&pHeaderCookie->CookieValue);
    pHeaderCookie->Size.RawValue = cbRequested;

    LPBYTE pReturnToCaller = (LPBYTE)pHeaderCookie + DOTNET_ALLOC_HEADER_COOKIE_SIZE_WITH_PADDING;

    UNALIGNED DOTNET_ALLOC_COOKIE* pTrailerCookie = (UNALIGNED DOTNET_ALLOC_COOKIE*)(pReturnToCaller + cbRequested);
    pTrailerCookie->CookieValue = EncodePointer(&pTrailerCookie->CookieValue);
    pTrailerCookie->Size.EncodedValue = EncodePointer((PVOID)cbRequested);

    return pReturnToCaller;
}

FORCEINLINE
void zcfree_trash_cookie(UNALIGNED DOTNET_ALLOC_COOKIE* pCookie)
{
    memset(pCookie, 0, sizeof(*pCookie));
    pCookie->CookieValue = (PVOID)(SIZE_T)0xDEADBEEF;
}

// Marked noinline to keep it on the call stack during crash reports.
DECLSPEC_NOINLINE
DECLSPEC_NORETURN
void zcfree_cookie_check_failed()
{
    __fastfail(FAST_FAIL_HEAP_METADATA_CORRUPTION);
}

void ZLIB_INTERNAL zcfree (opaque, ptr)
    voidpf opaque;
    voidpf ptr;
{
    (void)opaque;

    if (ptr == NULL) { return; } // ok to free nullptr

    // Check cookie at beginning and end

    DOTNET_ALLOC_COOKIE* pHeaderCookie = (DOTNET_ALLOC_COOKIE*)((LPBYTE)ptr - DOTNET_ALLOC_HEADER_COOKIE_SIZE_WITH_PADDING);
    if (DecodePointer(pHeaderCookie->CookieValue) != &pHeaderCookie->CookieValue) { goto Fail; }
    SIZE_T cbRequested = pHeaderCookie->Size.RawValue;

    UNALIGNED DOTNET_ALLOC_COOKIE* pTrailerCookie = (UNALIGNED DOTNET_ALLOC_COOKIE*)((LPBYTE)ptr + cbRequested);
    if (DecodePointer(pTrailerCookie->CookieValue) != &pTrailerCookie->CookieValue) { goto Fail; }
    if (DecodePointer(pTrailerCookie->Size.EncodedValue) != (LPVOID)cbRequested) { goto Fail; }

    // Checks passed - now trash the cookies and free memory

    zcfree_trash_cookie(pHeaderCookie);
    zcfree_trash_cookie(pTrailerCookie);

    if (!HeapFree(s_allocHeap, 0, pHeaderCookie)) { goto Fail; }
    return;

Fail:
    zcfree_cookie_check_failed();
}
