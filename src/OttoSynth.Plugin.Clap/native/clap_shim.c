// clap_shim.c — minimal C bridge between CLAP's required `clap_entry` global
// symbol and our managed (NativeAOT) implementation.
//
// CLAP requires a global variable named `clap_entry` of type `clap_plugin_entry_t`.
// NativeAOT can export FUNCTIONS via [UnmanagedCallersOnly], but not DATA
// (a struct). So we declare `clap_entry` here as a static const struct whose
// function-pointer fields point to our managed exports.
//
// The CLAP API surface area exposed via this shim is intentionally minimal:
//   - clap_entry → init / deinit / get_factory
//   - get_factory("clap.plugin-factory") → factory with get_plugin_count and create_plugin
//   - create_plugin() returns a managed-built clap_plugin_t*
//
// Everything else (extensions: audio_ports, note_ports, params, state, etc.)
// is wired up at runtime by the managed code via its own struct allocations.
//
// We only need C here for the SYMBOL EXPORT problem. All logic lives in C#.

#include <stdint.h>
#include <stddef.h>

// CLAP version we target.
#define CLAP_VERSION_MAJOR 1
#define CLAP_VERSION_MINOR 2
#define CLAP_VERSION_REVISION 0

#if defined(_WIN32)
  #define CLAP_EXPORT __declspec(dllexport)
  #define CLAP_ABI    __cdecl
#else
  #define CLAP_EXPORT __attribute__((visibility("default")))
  #define CLAP_ABI
#endif

typedef struct clap_version {
    uint32_t major;
    uint32_t minor;
    uint32_t revision;
} clap_version_t;

typedef struct clap_plugin_entry {
    clap_version_t clap_version;
    int  (CLAP_ABI *init)       (const char *plugin_path);
    void (CLAP_ABI *deinit)     (void);
    const void *(CLAP_ABI *get_factory)(const char *factory_id);
} clap_plugin_entry_t;

// Forward declarations of the managed exports. NativeAOT will emit these
// symbols from C# methods decorated with [UnmanagedCallersOnly(EntryPoint=...)].
extern int  CLAP_ABI OttoClap_Init(const char *plugin_path);
extern void CLAP_ABI OttoClap_Deinit(void);
extern const void * CLAP_ABI OttoClap_GetFactory(const char *factory_id);

// The CLAP entry point — global symbol the host looks up by name.
CLAP_EXPORT const clap_plugin_entry_t clap_entry = {
    { CLAP_VERSION_MAJOR, CLAP_VERSION_MINOR, CLAP_VERSION_REVISION },
    OttoClap_Init,
    OttoClap_Deinit,
    OttoClap_GetFactory
};
