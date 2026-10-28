"""
自动工作项处理器
后台定期检查新工作项并自动分析处理
"""
import time
import json
from datetime import datetime
from agent_core import VegaAgent
from model_manager import ModelManager
from tools.azure_devops import fetch_new_work_items


def load_config():
    """加载配置文件"""
    try:
        with open('config.json', 'r', encoding='utf-8') as f:
            return json.load(f)
    except Exception as e:
        print(f"[AutoProcessor] 配置加载失败: {e}")
        return {}


def auto_process_loop():
    """自动处理工作项的主循环"""
    config = load_config()
    auto_config = config.get('auto_processor', {})

    if not auto_config.get('enabled', False):
        print("[AutoProcessor] 自动处理未启用，请在 config.json 中设置 auto_processor.enabled = true")
        return

    interval_minutes = auto_config.get('interval_minutes', 10)
    print(f"[AutoProcessor] 启动自动处理器，检查间隔: {interval_minutes} 分钟")

    # 初始化 Agent
    model_manager = ModelManager()
    agent = VegaAgent(model_manager)

    # 记录已处理的工作项 ID
    processed_ids = set()

    while True:
        try:
            print(f"\n[AutoProcessor] {datetime.now().strftime('%Y-%m-%d %H:%M:%S')} - 检查新工作项...")

            # 获取新工作项
            result = fetch_new_work_items()

            if result.get('status') != 'success':
                print(f"[AutoProcessor] 获取工作项失败: {result.get('message', '未知错误')}")
                time.sleep(interval_minutes * 60)
                continue

            items = result.get('items', [])
            new_items = [item for item in items if item['id'] not in processed_ids]

            if not new_items:
                print(f"[AutoProcessor] 没有新的工作项 (共 {len(items)} 个已处理)")
            else:
                print(f"[AutoProcessor] 发现 {len(new_items)} 个新工作项")

                for item in new_items:
                    work_item_id = item['id']
                    title = item['title']

                    print(f"\n[AutoProcessor] 开始处理工作项 #{work_item_id}: {title}")

                    # 构建处理提示
                    prompt = f"""请自动分析并处理工作项 #{work_item_id}:

标题: {title}

请按照以下步骤执行:
1. 获取工作项详细信息
2. 分析问题描述和复现步骤
3. 搜索相关代码
4. 如果需要，连接 Vega.exe 进行运行时排查（vega_control_* 只读工具）
5. 确定性验证走闭环：先 `start_loop_baseline` 复现并记住界面；
   只有基线 verdict=FAIL 才允许修复；修复提交后 `finish_loop` 重编重验并对比界面指纹。
   用 get_loop_status 轮询。结论带 baseline/after 的 runId。
   停在人工闸门：不 push、不开 PR、不改工作项状态。
6. 生成详细的诊断报告，写明 loopVerdict 与 uiDrift（有控件增删或位移时明确要求人工确认）
7. 将报告发布到工作项评论。不要改工作项状态。

注意：
- 报告应包含问题分析、相关代码位置、根本原因、验证结论、建议方案和风险评估。
- 验证结论必须遵守「验证纪律」：verdict 四态原样转述；BLOCKED 是环境问题不是 Bug；
  引用结论必须带 runId，并给出同目录 `report.md` 路径（Runner 自动生成，不要另写结论）；
  builtFromCurrentSource=false 时注明"Runner 未自行重编"；存在本 SHA 独立构建/指纹证据时一并引用，
  不误称为旧二进制、不改写该字段；channels.l1Api.available=false 时
  交叉验证未发生，不得表述为"强验证"。
- 未登记用例时自行进入准备阶段，阅读需求/diff，补写真实测试、输入和断言，构建/发现、登记后再执行；不能只记录缺登记就结束。无法覆盖时保存实际尝试和具体缺口，不等待人工，不伪造通过。
"""

                    try:
                        # 让 Agent 处理
                        steps = agent.chat(prompt)

                        # 检查是否成功
                        success = any(
                            step.get('type') == 'tool_call' and
                            'post_diagnosis_result' in step.get('content', '')
                            for step in steps
                        )

                        if success:
                            print(f"[AutoProcessor] ✓ 工作项 #{work_item_id} 处理完成")
                            processed_ids.add(work_item_id)
                        else:
                            print(f"[AutoProcessor] ✗ 工作项 #{work_item_id} 处理未完成")

                    except Exception as e:
                        print(f"[AutoProcessor] ✗ 处理工作项 #{work_item_id} 时出错: {e}")

                    # 避免频繁调用 API
                    time.sleep(5)

        except KeyboardInterrupt:
            print("\n[AutoProcessor] 收到中断信号，正在退出...")
            break
        except Exception as e:
            print(f"[AutoProcessor] 错误: {e}")

        # 等待下一次检查
        print(f"[AutoProcessor] 等待 {interval_minutes} 分钟后进行下一次检查...")
        time.sleep(interval_minutes * 60)


if __name__ == '__main__':
    print("""
╔═══════════════════════════════════════════════════════════╗
║         VegaAgent 自动工作项处理器                        ║
║                                                           ║
║  自动检查 Azure DevOps 中的新建未分配工作项              ║
║  并使用 AI 进行代码分析和问题诊断                        ║
╚═══════════════════════════════════════════════════════════╝
    """)

    auto_process_loop()
