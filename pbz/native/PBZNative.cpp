#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <string>
#include <vector>
#include <cstring>
#include <LuaMadeSimple/LuaMadeSimple.hpp>

namespace {
using RC::LuaMadeSimple::Lua;
HANDLE mapping = nullptr;
unsigned char* memory = nullptr;
HANDLE collisionMapping = nullptr;
unsigned char* collision = nullptr;
LONG collisionRequest = 0;
constexpr size_t slotBytes = 2048;
constexpr size_t payloadBytes = slotBytes - 16;

bool openMapping() {
    if (memory) return true;
    mapping = CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, slotBytes * 2, L"Local\\PBZCraft.Native_v1");
    if (!mapping) return false;
    memory = static_cast<unsigned char*>(MapViewOfFile(mapping, FILE_MAP_ALL_ACCESS, 0, 0, slotBytes * 2));
    if (!memory) { CloseHandle(mapping); mapping = nullptr; return false; }
    return true;
}

int send(const Lua& lua) {
    const int count = lua.get_stack_size();
    if (!openMapping() || count < 1 || count > payloadBytes / sizeof(double)) { lua.set_bool(false); return 1; }
    double values[payloadBytes / sizeof(double)];
    for (int index = 0; index < count; ++index) values[index] = lua.get_number(1);
    const size_t length = count * sizeof(double);
    auto seq = reinterpret_cast<volatile LONG*>(memory);
    InterlockedIncrement(seq);
    *reinterpret_cast<DWORD*>(memory + 4) = static_cast<DWORD>(length);
    *reinterpret_cast<ULONGLONG*>(memory + 8) = GetTickCount64();
    std::memcpy(memory + 16, values, length);
    MemoryBarrier();
    InterlockedIncrement(seq);
    lua.set_bool(true);
    return 1;
}

int receive(const Lua& lua) {
    if (!openMapping()) { lua.set_nil(); return 1; }
    const auto slot = memory + slotBytes;
    for (int attempt = 0; attempt < 3; ++attempt) {
        const LONG seq = *reinterpret_cast<volatile LONG*>(slot);
        if (!seq || (seq & 1)) continue;
        const DWORD length = *reinterpret_cast<DWORD*>(slot + 4);
        const ULONGLONG stamp = *reinterpret_cast<ULONGLONG*>(slot + 8);
        if (length > payloadBytes || GetTickCount64() - stamp > 3000) break;
        const std::string copy(reinterpret_cast<const char*>(slot + 16), length);
        MemoryBarrier();
        if (seq == *reinterpret_cast<volatile LONG*>(slot)) { lua.set_string(copy); return 1; }
    }
    lua.set_nil();
    return 1;
}

int ping(const Lua& lua) { lua.set_string("PBZCraft native IPC v1; official LuaMadeSimple API"); return 1; }

bool openCollision() {
    if (collision) return true;
    collisionMapping = CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, 512, L"Local\\PBZCraft.Collision_v1");
    if (!collisionMapping) return false;
    collision = static_cast<unsigned char*>(MapViewOfFile(collisionMapping, FILE_MAP_ALL_ACCESS, 0, 0, 512));
    if (!collision) { CloseHandle(collisionMapping); collisionMapping = nullptr; return false; }
    ZeroMemory(collision, 512);
    *reinterpret_cast<DWORD*>(collision) = 0x435A4250;
    *reinterpret_cast<DWORD*>(collision + 4) = 1;
    return true;
}

int collisionPoll(const Lua& lua) {
    const bool active = lua.get_stack_size() == 1 && lua.get_number(1) == 1;
    if (!openCollision()) { lua.set_nil(); return 1; }
    *reinterpret_cast<DWORD*>(collision + 132) = active ? 1 : 0;
    *reinterpret_cast<ULONGLONG*>(collision + 16) = GetTickCount64();
    if (!active) { lua.set_nil(); return 1; }
    const LONG request = *reinterpret_cast<volatile LONG*>(collision + 8);
    if (!request || request == *reinterpret_cast<volatile LONG*>(collision + 12)) { lua.set_nil(); return 1; }
    MemoryBarrier();
    collisionRequest = request;
    const std::string copy(reinterpret_cast<const char*>(collision + 32), 72);
    lua.set_string(copy);
    return 1;
}

int collisionRespond(const Lua& lua) {
    if (!collision || lua.get_stack_size() != 4) { lua.set_bool(false); return 1; }
    double values[4];
    for (auto& value : values) value = lua.get_number(1);
    std::memcpy(collision + 104, values, 3 * sizeof(double));
    *reinterpret_cast<DWORD*>(collision + 128) = static_cast<DWORD>(values[3]);
    MemoryBarrier();
    InterlockedExchange(reinterpret_cast<volatile LONG*>(collision + 12), collisionRequest);
    lua.set_bool(true);
    return 1;
}
}

extern "C" __declspec(dllexport) int luaopen_pbznative(lua_State* state) {
    Lua lua(state);
    lua.register_function("PBZNative_Send", send);
    lua.register_function("PBZNative_Receive", receive);
    lua.register_function("PBZNative_Ping", ping);
    lua.register_function("PBZNative_CollisionPoll", collisionPoll);
    lua.register_function("PBZNative_CollisionRespond", collisionRespond);
    return 0;
}
