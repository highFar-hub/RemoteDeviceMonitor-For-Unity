const json=(response,status,body)=>response.status(status).json(body);
export default async function handler(request,response){
  const url=process.env.SUPABASE_URL,key=process.env.SUPABASE_SERVICE_ROLE_KEY;
  if(!url||!key)return json(response,503,{error:"数据库尚未配置"});
  const headers={apikey:key,Authorization:`Bearer ${key}`,"Content-Type":"application/json"};
  if(request.method==="GET"){
    const result=await fetch(`${url}/rest/v1/devices?select=*&order=last_seen.desc`,{headers});
    const body=await result.json();return json(response,result.status,result.ok?{devices:body}:{error:body});
  }
  if(request.method==="POST"){
    if(!process.env.DEVICE_API_KEY||request.headers["x-device-key"]!==process.env.DEVICE_API_KEY)return json(response,401,{error:"设备密钥错误"});
    const data=request.body||{};if(!data.device_id)return json(response,400,{error:"缺少 device_id"});
    let status={};
    try{status=typeof data.status_json==="string"?JSON.parse(data.status_json):(data.status||{});}catch{status={parseError:"设备状态 JSON 无法解析"};}
    const record={device_id:String(data.device_id).slice(0,100),device_name:String(data.device_name||data.device_id).slice(0,100),current_video:Number.isFinite(Number(data.current_video))?Number(data.current_video):null,is_playing:Boolean(data.is_playing),fps:Number.isFinite(Number(data.fps??status.fps))?Number(data.fps??status.fps):null,error_message:String(data.error_message||"").slice(0,1000),project_id:String(data.project_id||"video-demo").slice(0,100),project_name:String(data.project_name||"视频切换 Demo").slice(0,100),app_version:String(data.app_version||"").slice(0,50),scene_name:String(data.scene_name||"").slice(0,100),status,last_seen:new Date().toISOString()};
    const result=await fetch(`${url}/rest/v1/devices?on_conflict=device_id`,{method:"POST",headers:{...headers,Prefer:"resolution=merge-duplicates,return=representation"},body:JSON.stringify(record)});
    const body=await result.json();return json(response,result.status,result.ok?{ok:true,device:body[0]}:{error:body});
  }
  if(request.method==="PATCH"){
    if(!process.env.CONTROL_API_KEY||request.headers["x-control-key"]!==process.env.CONTROL_API_KEY)return json(response,401,{error:"控制密钥错误"});
    const data=request.body||{};
    const video=Number(data.desired_video);
    if(!data.device_id||!Number.isInteger(video)||video<1||video>5)return json(response,400,{error:"设备或视频编号无效"});
    const result=await fetch(`${url}/rest/v1/devices?device_id=eq.${encodeURIComponent(data.device_id)}`,{method:"PATCH",headers:{...headers,Prefer:"return=representation"},body:JSON.stringify({desired_video:video,desired_action:"play_video",command_payload:{video},command_updated_at:new Date().toISOString()})});
    const body=await result.json();return json(response,result.status,result.ok?{ok:true,device:body[0]}:{error:body});
  }
  response.setHeader("Allow","GET, POST, PATCH");return json(response,405,{error:"Method not allowed"});
}
