const json=(response,status,body)=>response.status(status).json(body);
export default async function handler(request,response){
  const url=process.env.SUPABASE_URL,key=process.env.SUPABASE_SERVICE_ROLE_KEY;
  if(!url||!key)return json(response,503,{error:"数据库尚未配置"});
  const headers={apikey:key,Authorization:`Bearer ${key}`,"Content-Type":"application/json"};
  if(request.method==="GET"){
    if(!process.env.DEVICE_API_KEY||request.headers["x-device-key"]!==process.env.DEVICE_API_KEY)return json(response,401,{error:"设备密钥错误"});
    const id=String(request.query.device_id||"");
    if(!id)return json(response,400,{error:"缺少 device_id"});
    const result=await fetch(`${url}/rest/v1/devices?device_id=eq.${encodeURIComponent(id)}&select=desired_video,desired_action,command_payload,command_updated_at&limit=1`,{headers});
    const body=await result.json();
    const command=body[0]||{desired_video:null,desired_action:null,command_payload:{},command_updated_at:null};
    if(command.command_payload&&typeof command.command_payload!=="string")command.command_payload=JSON.stringify(command.command_payload);
    return json(response,result.status,result.ok?command:{error:body});
  }
  if(request.method!=="POST")return json(response,405,{error:"Method not allowed"});
  if(!process.env.CONTROL_API_KEY||request.headers["x-control-key"]!==process.env.CONTROL_API_KEY)return json(response,401,{error:"控制密钥错误"});
  const data=request.body||{},id=String(data.device_id||""),action=String(data.action||"");
  const allowed=new Set(["play_video","projection_enabled","arduino_enabled","refresh_routing"]);
  if(!id||!allowed.has(action))return json(response,400,{error:"设备或命令无效"});
  const update={desired_action:action,command_payload:data.payload||{},command_updated_at:new Date().toISOString()};
  if(action==="play_video")update.desired_video=Number(data.payload?.video)||null;
  const result=await fetch(`${url}/rest/v1/devices?device_id=eq.${encodeURIComponent(id)}`,{method:"PATCH",headers:{...headers,Prefer:"return=representation"},body:JSON.stringify(update)});
  const body=await result.json();
  return json(response,result.status,result.ok?{ok:true,device:body[0]}:{error:body});
}
