"""
Azure DevOps 工作项集成工具
支持获取、分析和更新 Azure DevOps 工作项
"""
import os
import requests
import base64
import json
from datetime import datetime
from typing import List, Dict, Optional


class AzureDevOpsClient:
    """Azure DevOps API 客户端"""

    def __init__(self, org_url: str, pat: str, project: str):
        """
        初始化 Azure DevOps 客户端

        Args:
            org_url: 组织 URL，如 http://win-devops:8080/DefaultCollection
            pat: Personal Access Token
            project: 项目名称，如 Vega_SW
        """
        self.org_url = org_url.rstrip('/')
        self.project = project
        self.base_url = f"{self.org_url}/{project}/_apis"

        # 构建认证头
        auth_string = f":{pat}"
        b64_auth = base64.b64encode(auth_string.encode()).decode()
        self.headers = {
            'Content-Type': 'application/json',
            'Authorization': f'Basic {b64_auth}'
        }

    def get_new_unassigned_items(self) -> List[Dict]:
        """获取状态为'新建'且未分配的工作项"""
        wiql = {
            "query": """
                SELECT [System.Id], [System.Title], [System.State],
                       [System.AssignedTo], [System.AreaPath], [System.Tags],
                       [System.ChangedDate]
                FROM WorkItems
                WHERE [System.State] = '新建'
                  AND [System.AssignedTo] = ''
                ORDER BY [System.ChangedDate] DESC
            """
        }

        try:
            response = requests.post(
                f"{self.base_url}/wit/wiql?api-version=7.0",
                json=wiql,
                headers=self.headers,
                timeout=10
            )
            response.raise_for_status()
            work_items = response.json().get('workItems', [])

            # 批量获取详细信息
            if not work_items:
                return []

            ids = [str(item['id']) for item in work_items]
            details_response = requests.get(
                f"{self.base_url}/wit/workitems?ids={','.join(ids)}&api-version=7.0",
                headers=self.headers,
                timeout=10
            )
            details_response.raise_for_status()

            return details_response.json().get('value', [])

        except Exception as e:
            print(f"[AzureDevOps] 获取工作项失败: {e}")
            return []

    def get_work_item(self, work_item_id: int) -> Optional[Dict]:
        """获取指定工作项的详细信息"""
        try:
            response = requests.get(
                f"{self.base_url}/wit/workitems/{work_item_id}?api-version=7.0",
                headers=self.headers,
                timeout=10
            )
            response.raise_for_status()
            return response.json()
        except Exception as e:
            print(f"[AzureDevOps] 获取工作项 {work_item_id} 失败: {e}")
            return None

    def add_comment(self, work_item_id: int, comment: str) -> bool:
        """添加评论到工作项"""
        try:
            payload = {"text": comment}
            response = requests.post(
                f"{self.base_url}/wit/workitems/{work_item_id}/comments?api-version=7.0-preview.3",
                json=payload,
                headers=self.headers,
                timeout=10
            )
            response.raise_for_status()
            return True
        except Exception as e:
            print(f"[AzureDevOps] 添加评论失败: {e}")
            return False

    def update_work_item(self, work_item_id: int, fields: Dict) -> bool:
        """
        更新工作项字段

        Args:
            work_item_id: 工作项 ID
            fields: 要更新的字段，如 {"System.State": "进行中", "System.AssignedTo": "user@example.com"}
        """
        try:
            operations = [
                {"op": "add", "path": f"/fields/{k}", "value": v}
                for k, v in fields.items()
            ]
            response = requests.patch(
                f"{self.base_url}/wit/workitems/{work_item_id}?api-version=7.0",
                json=operations,
                headers={'Content-Type': 'application/json-patch+json', **self.headers},
                timeout=10
            )
            response.raise_for_status()
            return True
        except Exception as e:
            print(f"[AzureDevOps] 更新工作项失败: {e}")
            return False


# 全局客户端实例（延迟初始化）
_client: Optional[AzureDevOpsClient] = None


def _get_client() -> AzureDevOpsClient:
    """获取或创建 Azure DevOps 客户端"""
    global _client
    if _client is None:
        # 配置文件按脚本目录定位（不依赖进程 CWD）
        config_path = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'config.json')
        ado_config = {}
        try:
            with open(config_path, 'r', encoding='utf-8') as f:
                ado_config = json.load(f).get('azure_devops', {})
        except Exception as e:
            print(f"[AzureDevOps] 配置加载失败({config_path}): {e}")

        # PAT 安全策略：环境变量优先，config.json 回退（保持既有部署兼容）。
        # 明文 pat 只应出现在本地 config.json（已 gitignore），仓库内不得出现。
        pat = os.environ.get("AZURE_DEVOPS_PAT") or ado_config.get('pat', '')
        if not pat:
            print("[AzureDevOps] 警告: 未配置 PAT（set AZURE_DEVOPS_PAT=... 或 config.json azure_devops.pat）")

        _client = AzureDevOpsClient(
            org_url=ado_config.get('org_url', 'http://win-devops:8080/DefaultCollection'),
            pat=pat,
            project=ado_config.get('project', 'Vega_SW')
        )
    return _client


# ==========================================
# Tool Functions
# ==========================================

def fetch_new_work_items() -> dict:
    """获取状态为'新建'且未分配的工作项列表"""
    client = _get_client()
    items = client.get_new_unassigned_items()

    result_items = []
    for item in items:
        fields = item.get('fields', {})
        work_item_id = item.get('id')
        result_items.append({
            "id": work_item_id,
            "title": fields.get('System.Title', ''),
            "state": fields.get('System.State', ''),
            "area_path": fields.get('System.AreaPath', ''),
            "tags": fields.get('System.Tags', ''),
            "changed_date": fields.get('System.ChangedDate', ''),
            "url": f"{client.org_url}/{client.project}/_workitems/edit/{work_item_id}"
        })

    return {
        "status": "success",
        "count": len(result_items),
        "items": result_items
    }


def get_work_item_detail(work_item_id: int) -> dict:
    """获取指定工作项的详细信息"""
    client = _get_client()
    item = client.get_work_item(work_item_id)

    if not item:
        return {
            "status": "error",
            "message": f"无法获取工作项 #{work_item_id}"
        }

    fields = item.get('fields', {})

    # 提取关键信息
    detail = {
        "status": "success",
        "id": item.get('id'),
        "title": fields.get('System.Title', ''),
        "description": fields.get('System.Description', ''),
        "repro_steps": fields.get('Microsoft.VSTS.TCM.ReproSteps', ''),
        "acceptance_criteria": fields.get('Microsoft.VSTS.Common.AcceptanceCriteria', ''),
        "state": fields.get('System.State', ''),
        "assigned_to": fields.get('System.AssignedTo', {}).get('displayName', '未分配'),
        "area_path": fields.get('System.AreaPath', ''),
        "tags": fields.get('System.Tags', ''),
        "priority": fields.get('Microsoft.VSTS.Common.Priority', ''),
        "created_date": fields.get('System.CreatedDate', ''),
        "changed_date": fields.get('System.ChangedDate', ''),
        "url": f"{client.org_url}/{client.project}/_workitems/edit/{item.get('id')}"
    }

    return detail


def post_diagnosis_result(work_item_id: int, result: str) -> dict:
    """将诊断结果发布到工作项评论"""
    client = _get_client()

    # 格式化评论
    timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    formatted_result = f"""## 🤖 VegaAgent 自动诊断报告

{result}

---
*此报告由 VegaAgent 自动生成于 {timestamp}*
"""

    success = client.add_comment(work_item_id, formatted_result)

    if success:
        return {
            "status": "success",
            "message": f"诊断结果已发布到工作项 #{work_item_id}"
        }
    else:
        return {
            "status": "error",
            "message": "发布评论失败，请检查网络连接和权限"
        }


def update_work_item_status(work_item_id: int, state: str, assigned_to: str = None) -> dict:
    """
    更新工作项状态（受策略层拦截：policy.json allowWorkItemStateChange /
    allowWorkItemAssign=false 时直接拒绝——代码级强制，不依赖提示词）

    Args:
        work_item_id: 工作项 ID
        state: 新状态，如 "进行中", "已完成", "已关闭"
        assigned_to: 可选，指派给的用户邮箱
    """
    from tools.policy import check_work_item_state_change, check_work_item_assign

    gate = check_work_item_state_change()
    if not gate.get("allowed"):
        return {"status": "blocked", "message": gate["reason"]}

    if assigned_to:
        gate = check_work_item_assign()
        if not gate.get("allowed"):
            return {"status": "blocked", "message": gate["reason"]}

    client = _get_client()

    fields = {"System.State": state}
    if assigned_to:
        fields["System.AssignedTo"] = assigned_to

    success = client.update_work_item(work_item_id, fields)

    if success:
        return {
            "status": "success",
            "message": f"工作项 #{work_item_id} 已更新"
        }
    else:
        return {
            "status": "error",
            "message": "更新工作项失败"
        }


# ==========================================
# Tool Map & Schema
# ==========================================

TOOL_MAP = {
    "fetch_new_work_items": lambda args: fetch_new_work_items(),
    "get_work_item_detail": lambda args: get_work_item_detail(args["work_item_id"]),
    "post_diagnosis_result": lambda args: post_diagnosis_result(
        args["work_item_id"], args["result"]),
    "update_work_item_status": lambda args: update_work_item_status(
        args["work_item_id"], args["state"], args.get("assigned_to")),
}

TOOLS_SCHEMA = [
    {
        "type": "function",
        "function": {
            "name": "fetch_new_work_items",
            "description": "获取 Azure DevOps 中状态为'新建'且未分配的工作项列表",
            "parameters": {
                "type": "object",
                "properties": {}
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_work_item_detail",
            "description": "获取指定工作项的详细信息（标题、描述、复现步骤、验收标准等）",
            "parameters": {
                "type": "object",
                "properties": {
                    "work_item_id": {
                        "type": "integer",
                        "description": "工作项 ID"
                    }
                },
                "required": ["work_item_id"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "post_diagnosis_result",
            "description": "将诊断结果发布到工作项的评论区（支持 Markdown 格式）",
            "parameters": {
                "type": "object",
                "properties": {
                    "work_item_id": {
                        "type": "integer",
                        "description": "工作项 ID"
                    },
                    "result": {
                        "type": "string",
                        "description": "诊断结果内容，支持 Markdown 格式"
                    }
                },
                "required": ["work_item_id", "result"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "update_work_item_status",
            "description": "更新工作项的状态和指派人",
            "parameters": {
                "type": "object",
                "properties": {
                    "work_item_id": {
                        "type": "integer",
                        "description": "工作项 ID"
                    },
                    "state": {
                        "type": "string",
                        "description": "新状态，如 '进行中', '已完成', '已关闭'"
                    },
                    "assigned_to": {
                        "type": "string",
                        "description": "可选，指派给的用户邮箱"
                    }
                },
                "required": ["work_item_id", "state"]
            }
        }
    }
]
