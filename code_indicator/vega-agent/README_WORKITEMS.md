# VegaAgent - Azure DevOps 工作项集成

## 新增功能

### 1. Azure DevOps 工作项自动处理

VegaAgent 现在支持自动获取、分析和处理 Azure DevOps 工作项。

### 2. 配置说明

编辑 `config.json` 文件：

```json
{
  "azure_devops": {
    "org_url": "http://win-devops:8080/DefaultCollection",
    "project": "Vega_SW",
    "pat": "YOUR_PERSONAL_ACCESS_TOKEN_HERE"
  },
  "auto_processor": {
    "enabled": false,
    "interval_minutes": 10
  }
}
```

**获取 Personal Access Token (PAT):**

1. 访问 Azure DevOps: `http://win-devops:8080/DefaultCollection`
2. 点击右上角用户头像 → "安全性" → "个人访问令牌"
3. 点击 "新建令牌"
4. 设置权限：
   - Work Items: Read & Write
   - Code: Read (用于代码搜索)
5. 复制生成的 Token 并粘贴到 `config.json` 的 `pat` 字段

### 3. 使用方式

#### 方式一：手动模式（推荐）

启动 VegaAgent 桌面应用：

```bash
python app.py
```

在界面中：
- 左侧边栏会显示"待处理工作项"面板
- 点击工作项卡片自动发送分析请求
- 或直接输入："请分析工作项 #2471"
- 或点击"检查工作项"快捷按钮

#### 方式二：自动模式

启动后台自动处理器：

```bash
python auto_processor.py
```

自动处理器会：
1. 每隔 N 分钟检查新工作项
2. 自动分析问题并搜索相关代码
3. 生成诊断报告并发布到工作项评论
4. 记录已处理的工作项避免重复

**启用自动模式：**
在 `config.json` 中设置 `auto_processor.enabled = true`

### 4. Agent 工作流程

当你说"请分析工作项 #ID"时，Agent 会自动：

1. **获取工作项详情**
   - 标题、描述、复现步骤、验收标准

2. **代码检索**
   - 根据关键词搜索相关代码
   - 追踪类的定义和调用关系
   - 定位可能的问题代码

3. **运行时排查（如需要）**
   - 连接 Vega.exe 获取系统状态
   - 验证问题是否可复现

4. **生成诊断报告**
   - 问题分析
   - 相关代码位置
   - 根本原因
   - 建议方案
   - 风险评估

5. **发布结果**
   - 自动发布到工作项评论区
   - 支持 Markdown 格式

### 5. 新增工具函数

- `fetch_new_work_items()` - 获取新建未分配的工作项列表
- `get_work_item_detail(work_item_id)` - 获取工作项详细信息
- `post_diagnosis_result(work_item_id, result)` - 发布诊断结果
- `update_work_item_status(work_item_id, state, assigned_to)` - 更新工作项状态

### 6. 前端界面更新

- 左侧边栏新增"待处理工作项"面板
- 实时显示新建未分配的工作项
- 点击工作项卡片快速分析
- 每分钟自动刷新列表

### 7. 示例对话

**用户:** 检查新的工作项

**Agent:**
- 调用 `fetch_new_work_items()`
- 发现工作项 #2471: "部分标签页在理想状态的'除盘之外全部关闭'会关闭所有有关的标签页"
- 调用 `get_work_item_detail(2471)`
- 搜索相关代码: `search_code("除盘之外全部关闭")`
- 追踪类: `trace_class("TabManager")`
- 生成报告并发布: `post_diagnosis_result(2471, report)`

### 8. 注意事项

- 确保网络可以访问 `http://win-devops:8080`
- PAT 需要有足够的权限（Work Items: Read & Write）
- 自动模式会持续运行，建议在服务器上使用
- 手动模式更适合日常开发使用

### 9. 故障排查

**无法连接 Azure DevOps:**
- 检查 `org_url` 是否正确
- 确认网络连接
- 验证 PAT 是否有效

**工作项列表为空:**
- 确认有"新建"且"未分配"的工作项
- 检查 PAT 权限
- 查看浏览器控制台错误信息

**Agent 分析不准确:**
- 提供更详细的工作项描述
- 在复现步骤中包含关键信息
- 手动指导 Agent 搜索特定代码

## 技术实现

- **后端:** Flask + Azure DevOps REST API
- **认证:** Basic Auth with PAT
- **前端:** 原生 JavaScript + Fetch API
- **Agent:** LangGraph 多轮对话 + Function Calling
