cmake_minimum_required(VERSION 3.22)

project(SprayerControllerHostTests LANGUAGES C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_C_EXTENSIONS OFF)

enable_testing()

function(add_host_test target source)
    add_executable(${target}
        ${source}
        ${CMAKE_CURRENT_LIST_DIR}/../App/host_registers.c
        ${CMAKE_CURRENT_LIST_DIR}/../App/config_store.c
        ${CMAKE_CURRENT_LIST_DIR}/../App/pwm_engine.c
        ${CMAKE_CURRENT_LIST_DIR}/../App/modbus_crc.c
        ${CMAKE_CURRENT_LIST_DIR}/../App/host_modbus.c
        ${CMAKE_CURRENT_LIST_DIR}/../App/current_module.c
        ${CMAKE_CURRENT_LIST_DIR}/../App/app_runtime.c)
    target_include_directories(${target} PRIVATE ${CMAKE_CURRENT_LIST_DIR}/../App)
    if(MSVC)
        target_compile_options(${target} PRIVATE /W4 /WX /utf-8)
    else()
        target_compile_options(${target} PRIVATE -Wall -Wextra -Werror)
    endif()
    add_test(NAME ${target} COMMAND ${target})
endfunction()

add_host_test(test_host_registers test_host_registers.c)
add_host_test(test_config_store test_config_store.c)
