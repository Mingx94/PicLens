const fs=require('node:fs');
const {run_external_command}=require('/home/michael/.codex/plugins/cache/agentflow/agentflow/local/skills/agentflow/scripts/external-runner.js');
const r='/home/michael/Work/PicLens/.agentflow/artifacts/A-004-arch-remaining';
const brief=fs.readFileSync(r+'/cross-check-brief-2.md','utf8');
if(!brief.includes('model gpt-5.6-sol; effort low.'))throw Error('dispatch identity mismatch');
(async()=>{const result=await run_external_command({source_directory:'/home/michael/Work/PicLens',clone_directory:'/tmp/piclens-A004-crosscheck-records/clone',command:['codex','exec','--ephemeral','--model','gpt-5.6-sol','-c','model_reasoning_effort="low"','--sandbox','workspace-write',brief],result_file:'cross-check-report.md',max_output_bytes:4096});fs.writeFileSync(r+'/cross-check-runner-2.json',JSON.stringify(result,null,2)+'\n');process.stdout.write(JSON.stringify({status:result.status,exit_code:result.exit_code,result_file:result.result_file})+'\n');})().catch(e=>{process.stderr.write(String(e));process.exitCode=1});
