"use client"

import * as React from "react"
import { GripVerticalIcon } from "lucide-react"
import * as ResizablePrimitive from "react-resizable-panels"

import { cn } from "@/lib/utils"

type ResizableExports = {
  Group?: React.ComponentType<any>
  PanelGroup?: React.ComponentType<any>
  Panel?: React.ComponentType<any>
  Separator?: React.ComponentType<any>
  PanelResizeHandle?: React.ComponentType<any>
}

const primitives = ResizablePrimitive as unknown as ResizableExports
const panelGroupPrimitive = primitives.PanelGroup ?? primitives.Group
const panelPrimitive = primitives.Panel
const panelResizeHandlePrimitive = primitives.PanelResizeHandle ?? primitives.Separator

if (!panelGroupPrimitive || !panelPrimitive || !panelResizeHandlePrimitive) {
  throw new Error("Unsupported react-resizable-panels version")
}

const PanelGroupPrimitive = panelGroupPrimitive as React.ComponentType<any>
const PanelPrimitive = panelPrimitive as React.ComponentType<any>
const PanelResizeHandlePrimitive = panelResizeHandlePrimitive as React.ComponentType<any>

function ResizablePanelGroup({
  className,
  ...props
}: { className?: string } & Record<string, unknown>) {
  return (
    <PanelGroupPrimitive
      data-slot="resizable-panel-group"
      className={cn(
        "flex h-full w-full data-[panel-group-direction=vertical]:flex-col",
        className
      )}
      {...(props as Record<string, unknown>)}
    />
  )
}

function ResizablePanel(props: Record<string, unknown>) {
  return <PanelPrimitive data-slot="resizable-panel" {...props} />
}

function ResizableHandle({
  withHandle,
  className,
  ...props
}: { withHandle?: boolean; className?: string } & Record<string, unknown>) {
  return (
    <PanelResizeHandlePrimitive
      data-slot="resizable-handle"
      className={cn(
        "bg-border focus-visible:ring-ring relative flex w-px items-center justify-center after:absolute after:inset-y-0 after:left-1/2 after:w-1 after:-translate-x-1/2 focus-visible:ring-1 focus-visible:ring-offset-1 focus-visible:outline-hidden data-[panel-group-direction=vertical]:h-px data-[panel-group-direction=vertical]:w-full data-[panel-group-direction=vertical]:after:left-0 data-[panel-group-direction=vertical]:after:h-1 data-[panel-group-direction=vertical]:after:w-full data-[panel-group-direction=vertical]:after:-translate-y-1/2 data-[panel-group-direction=vertical]:after:translate-x-0 [&[data-panel-group-direction=vertical]>div]:rotate-90",
        className
      )}
      {...(props as Record<string, unknown>)}
    >
      {withHandle && (
        <div className="bg-border z-10 flex h-4 w-3 items-center justify-center rounded-xs border">
          <GripVerticalIcon className="size-2.5" />
        </div>
      )}
    </PanelResizeHandlePrimitive>
  )
}

export { ResizablePanelGroup, ResizablePanel, ResizableHandle }
