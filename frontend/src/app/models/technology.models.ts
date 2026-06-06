export interface TechnologyDto {
  id: number;
  name: string;
  displayName: string;
  category: string;
  lifecycleStage: 'Emerging' | 'Growing' | 'Mature' | 'Declining' | 'Legacy';
  totalMentions: number;
  weeklyMentions: number;
  growthRate: number;
  momentumScore: number;
  demandScore: number;
  competitionScore: number;
  opportunityScore: number;
  emergingScore: number;
  industryCoverageCount: number;
  clusterCoverageCount: number;
  isAiRelated: boolean;
  isCloudRelated: boolean;
  isLegacy: boolean;
  avgLeadScore: number;
  avgUrgency: number;
  firstSeenAt: string;
  lastSeenAt: string;
  updatedAt: string;
}

export interface TechnologyRelationshipDto {
  technologyId: number;
  name: string;
  displayName: string;
  category: string;
  coOccurrenceCount: number;
  correlationScore: number;
  industryAffinity: string;
  aiAffinity: boolean;
}

export interface TechnologyDetailDto extends TechnologyDto {
  relationships: TechnologyRelationshipDto[];
}

export interface TechGraphNode {
  id: number;
  name: string;
  displayName: string;
  category: string;
  lifecycleStage: string;
  totalMentions: number;
  opportunityScore: number;
  isAiRelated: boolean;
}

export interface TechGraphEdge {
  source: number;
  target: number;
  coOccurrenceCount: number;
  correlationScore: number;
}

export interface TechnologyGraphDto {
  nodes: TechGraphNode[];
  edges: TechGraphEdge[];
}

export interface TechRebuildResultDto {
  technologiesUpserted: number;
  relationshipsUpserted: number;
  snapshotsAdded: number;
  jobsProcessed: number;
  duration: string;
  ranAt: string;
}

export interface PagedTechnologyResponse {
  items: TechnologyDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface IndustryTechDto {
  industry: string;
  topTechnologies: TechnologyDto[];
}

// Lifecycle stage color utility
export function lifecycleColor(stage: string): { bg: string; text: string; border: string } {
  switch (stage) {
    case 'Emerging':  return { bg: 'bg-sky-50',    text: 'text-sky-700',    border: 'border-sky-200' };
    case 'Growing':   return { bg: 'bg-green-50',  text: 'text-green-700',  border: 'border-green-200' };
    case 'Declining': return { bg: 'bg-amber-50',  text: 'text-amber-700',  border: 'border-amber-200' };
    case 'Legacy':    return { bg: 'bg-red-50',    text: 'text-red-700',    border: 'border-red-200' };
    default:          return { bg: 'bg-slate-100', text: 'text-slate-600',  border: 'border-slate-200' };
  }
}

// D3 node color (hex) by lifecycle stage
export function lifecycleHex(stage: string): string {
  switch (stage) {
    case 'Emerging':  return '#0ea5e9';
    case 'Growing':   return '#22c55e';
    case 'Declining': return '#f59e0b';
    case 'Legacy':    return '#ef4444';
    default:          return '#94a3b8';
  }
}

// Category badge color
export function categoryColor(cat: string): string {
  switch (cat) {
    case 'AI':           return 'bg-purple-100 text-purple-700';
    case 'Cloud':        return 'bg-sky-100 text-sky-700';
    case 'Backend':      return 'bg-blue-100 text-blue-700';
    case 'Frontend':     return 'bg-indigo-100 text-indigo-700';
    case 'Database':     return 'bg-orange-100 text-orange-700';
    case 'DevOps':       return 'bg-teal-100 text-teal-700';
    case 'Architecture': return 'bg-violet-100 text-violet-700';
    case 'Observability':return 'bg-pink-100 text-pink-700';
    case 'Security':     return 'bg-red-100 text-red-700';
    case 'Messaging':    return 'bg-yellow-100 text-yellow-700';
    default:             return 'bg-slate-100 text-slate-600';
  }
}
