let featureStatus='all';
let matrixKind='host';
let expandAll=false;
const container=document.getElementById('feature-groups');
for(const group of featureGroups){
 const section=document.createElement('section');section.className='feature-group';
 const title=document.createElement('h3');title.className='group-title';title.textContent=group.name;section.append(title);
 for(const [name,subtitle,status,icon,description] of group.items){
  const detail=document.createElement('details');detail.className='feature';detail.dataset.status=status;detail.dataset.search=(name+' '+subtitle+' '+description).toLowerCase();
  const summary=document.createElement('summary');
  const symbol=document.createElement('span');symbol.className='feature-icon';symbol.ariaHidden='true';symbol.textContent=icon;
  const text=document.createElement('span');text.className='feature-title';
  const strong=document.createElement('strong');strong.textContent=name;
  const small=document.createElement('small');small.textContent=subtitle;text.append(strong,small);
  const badge=document.createElement('span');badge.className='badge '+status;badge.textContent=status[0].toUpperCase()+status.slice(1);
  summary.append(symbol,text,badge);detail.append(summary);
  const body=document.createElement('div');body.className='feature-body';const p=document.createElement('p');p.textContent=description;body.append(p);detail.append(body);section.append(detail);
 }
 container.append(section);
}
function filterFeatures(){
 const query=document.getElementById('feature-search').value.trim().toLowerCase();let visible=0;
 for(const group of container.querySelectorAll('.feature-group')){
  let count=0;
  for(const feature of group.querySelectorAll('.feature')){
   const statusMatch=featureStatus==='all'||(featureStatus==='available'?feature.dataset.status!=='planned':feature.dataset.status==='planned');
   feature.hidden=!statusMatch||!feature.dataset.search.includes(query);if(!feature.hidden)count++;
  }
  group.hidden=count===0;visible+=count;
 }
 document.getElementById('feature-empty').hidden=visible>0;
}
document.getElementById('feature-search').addEventListener('input',filterFeatures);
document.querySelectorAll('[data-feature-filter]').forEach(button=>button.addEventListener('click',()=>{
 featureStatus=button.dataset.featureFilter;
 document.querySelectorAll('[data-feature-filter]').forEach(b=>{b.classList.toggle('active',b===button);b.setAttribute('aria-pressed',String(b===button));});filterFeatures();
}));
document.getElementById('expand-all').addEventListener('click',event=>{
 expandAll=!expandAll;container.querySelectorAll('.feature:not([hidden])').forEach(feature=>feature.open=expandAll);event.target.textContent=expandAll?'Collapse all −':'Expand all +';
});
function renderMatrix(){
 const matrix=matrices[matrixKind];const query=document.getElementById('matrix-search').value.trim().toLowerCase();
 const head=document.getElementById('matrix-head');const body=document.getElementById('matrix-body');head.replaceChildren();body.replaceChildren();
 const tr=document.createElement('tr');for(const label of matrix.head){const th=document.createElement('th');th.scope='col';th.textContent=label;tr.append(th);}head.append(tr);
 let visible=0;
 for(const [name,status,style,notes] of matrix.rows){
  if(!(name+' '+status+' '+notes).toLowerCase().includes(query))continue;
  const row=document.createElement('tr');const label=document.createElement('td');label.textContent=name;
  const state=document.createElement('td');const badge=document.createElement('span');badge.className='badge '+style;badge.textContent=status;state.append(badge);
  const note=document.createElement('td');note.textContent=notes;row.append(label,state,note);body.append(row);visible++;
 }
 document.getElementById('matrix-empty').hidden=visible>0;
 document.getElementById('compat-panel').setAttribute('aria-labelledby','tab-'+matrixKind);
}
document.querySelectorAll('[data-matrix]').forEach(button=>{
 button.addEventListener('click',()=>{
  matrixKind=button.dataset.matrix;document.querySelectorAll('[data-matrix]').forEach(b=>{b.classList.toggle('active',b===button);b.setAttribute('aria-selected',String(b===button));b.tabIndex=b===button?0:-1;});renderMatrix();
 });
 button.addEventListener('keydown',event=>{
  const buttons=[...document.querySelectorAll('[data-matrix]')];const index=buttons.indexOf(button);
  let next=event.key==='ArrowRight'?(index+1)%buttons.length:event.key==='ArrowLeft'?(index+buttons.length-1)%buttons.length:event.key==='Home'?0:event.key==='End'?buttons.length-1:null;
  if(next!==null){event.preventDefault();buttons[next].focus();buttons[next].click();}
 });
 button.tabIndex=button.dataset.matrix==='host'?0:-1;
});
document.getElementById('matrix-search').addEventListener('input',renderMatrix);
renderMatrix();
