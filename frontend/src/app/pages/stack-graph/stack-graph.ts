import {
  AfterViewInit, ChangeDetectionStrategy, ChangeDetectorRef,
  Component, ElementRef, OnDestroy, OnInit, ViewChild, inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import * as d3 from 'd3';
import { TechnologyService } from '../../services/technology.service';
import { TechGraphEdge, TechGraphNode, TechnologyDto, lifecycleHex } from '../../models/technology.models';

interface D3Node extends TechGraphNode, d3.SimulationNodeDatum {
  x?: number;
  y?: number;
}

interface D3Link extends d3.SimulationLinkDatum<D3Node> {
  coOccurrenceCount: number;
  correlationScore: number;
}

@Component({
  selector: 'app-stack-graph',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  templateUrl: './stack-graph.html',
})
export class StackGraphPage implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('svgContainer') svgContainerRef!: ElementRef<HTMLDivElement>;

  private readonly svc = inject(TechnologyService);
  private readonly cdr = inject(ChangeDetectorRef);

  loading = true;
  error = false;
  selectedTech: TechGraphNode | null = null;
  selectedCategory = '';

  private nodes: D3Node[] = [];
  private links: D3Link[] = [];
  private simulation: d3.Simulation<D3Node, D3Link> | null = null;

  readonly categories = ['', 'AI', 'Backend', 'Frontend', 'Cloud', 'Database', 'DevOps', 'Architecture', 'Observability', 'Security', 'Messaging'];

  readonly stageLegend = [
    { stage: 'Emerging', color: '#0ea5e9' },
    { stage: 'Growing', color: '#22c55e' },
    { stage: 'Mature', color: '#94a3b8' },
    { stage: 'Declining', color: '#f59e0b' },
    { stage: 'Legacy', color: '#ef4444' },
  ];

  ngOnInit(): void {
    this.svc.getGraph().subscribe({
      next: (data) => {
        this.nodes = data.nodes.map(n => ({ ...n })) as D3Node[];
        this.links = data.edges.map(e => ({
          source: e.source,
          target: e.target,
          coOccurrenceCount: e.coOccurrenceCount,
          correlationScore: e.correlationScore,
        })) as D3Link[];
        this.loading = false;
        this.cdr.markForCheck();
        // D3 render happens in AfterViewInit; we need to trigger it after view update
        setTimeout(() => this.renderGraph(), 50);
      },
      error: () => {
        this.loading = false;
        this.error = true;
        this.cdr.markForCheck();
      }
    });
  }

  ngAfterViewInit(): void {
    if (!this.loading) this.renderGraph();
  }

  ngOnDestroy(): void {
    this.simulation?.stop();
  }

  setCategory(cat: string): void {
    this.selectedCategory = cat;
    this.selectedTech = null;
    this.cdr.markForCheck();
    setTimeout(() => this.renderGraph(), 0);
  }

  private renderGraph(): void {
    const container = this.svgContainerRef?.nativeElement;
    if (!container || this.nodes.length === 0) return;

    // Clear previous render
    d3.select(container).selectAll('*').remove();
    this.simulation?.stop();

    const width = container.clientWidth || 800;
    const height = 580;

    const visibleNodes = this.selectedCategory
      ? this.nodes.filter(n => n.category === this.selectedCategory)
      : this.nodes;

    const visibleIds = new Set(visibleNodes.map(n => n.id));

    const visibleLinks = this.links.filter(
      l => visibleIds.has(+l.source as unknown as number) && visibleIds.has(+l.target as unknown as number)
    );

    const nodeRadius = (d: D3Node) =>
      Math.max(8, Math.min(40, Math.log(d.totalMentions + 1) * 5));

    const svg = d3.select(container)
      .append('svg')
      .attr('width', '100%')
      .attr('height', height)
      .attr('viewBox', `0 0 ${width} ${height}`)
      .call(
        d3.zoom<SVGSVGElement, unknown>()
          .scaleExtent([0.3, 4])
          .on('zoom', (event) => g.attr('transform', event.transform))
      );

    const g = svg.append('g');

    // Links
    const link = g.append('g')
      .selectAll<SVGLineElement, D3Link>('line')
      .data(visibleLinks)
      .join('line')
      .attr('stroke', '#cbd5e1')
      .attr('stroke-opacity', 0.6)
      .attr('stroke-width', d => Math.max(0.5, Math.min(5, d.correlationScore / 20)));

    // Nodes
    const node = g.append('g')
      .selectAll<SVGGElement, D3Node>('g')
      .data(visibleNodes)
      .join('g')
      .style('cursor', 'pointer')
      .call(
        d3.drag<SVGGElement, D3Node>()
          .on('start', (event, d) => {
            if (!event.active) this.simulation?.alphaTarget(0.3).restart();
            d.fx = d.x;
            d.fy = d.y;
          })
          .on('drag', (event, d) => {
            d.fx = event.x;
            d.fy = event.y;
          })
          .on('end', (event, d) => {
            if (!event.active) this.simulation?.alphaTarget(0);
            d.fx = null;
            d.fy = null;
          })
      )
      .on('click', (_, d) => {
        this.selectedTech = d;
        this.cdr.markForCheck();
      });

    node.append('circle')
      .attr('r', nodeRadius)
      .attr('fill', d => lifecycleHex(d.lifecycleStage))
      .attr('fill-opacity', 0.85)
      .attr('stroke', '#fff')
      .attr('stroke-width', 1.5);

    node.append('text')
      .text(d => d.name)
      .attr('text-anchor', 'middle')
      .attr('dy', d => nodeRadius(d) + 11)
      .attr('font-size', '9px')
      .attr('fill', '#64748b')
      .attr('pointer-events', 'none');

    this.simulation = d3.forceSimulation<D3Node>(visibleNodes)
      .force('link', d3.forceLink<D3Node, D3Link>(visibleLinks)
        .id(d => d.id)
        .strength(d => d.correlationScore / 200)
        .distance(80))
      .force('charge', d3.forceManyBody<D3Node>().strength(-160))
      .force('center', d3.forceCenter(width / 2, height / 2))
      .force('collision', d3.forceCollide<D3Node>(d => nodeRadius(d) + 6))
      .on('tick', () => {
        link
          .attr('x1', d => (d.source as D3Node).x ?? 0)
          .attr('y1', d => (d.source as D3Node).y ?? 0)
          .attr('x2', d => (d.target as D3Node).x ?? 0)
          .attr('y2', d => (d.target as D3Node).y ?? 0);

        node.attr('transform', d => `translate(${d.x ?? 0},${d.y ?? 0})`);
      });
  }
}
