#pragma once

/* 初始化业务层、RS485 接收和 1 ms 定时器。 */
void AppHal_Init(void);
/* 主循环中持续调用：推进通信状态机、保存配置并喂独立看门狗。 */
void AppHal_Poll(void);
