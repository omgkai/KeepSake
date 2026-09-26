import SwiftUI

struct WorkspaceEmptyState<Actions:View>:View {
    @Environment(\.gameTheme) private var theme
    let title:String,icon:String,message:String
    @ViewBuilder var actions:()->Actions
    var body:some View {
        GeometryReader { space in
            VStack(spacing:space.size.height<300 ? 10:18) {
                if space.size.height>260 { Image(systemName:icon).font(.system(size:38,weight:.light)).foregroundStyle(theme.accent)
                    .frame(width:88,height:88).background(theme.accent.opacity(0.10),in:RoundedRectangle(cornerRadius:28)) }
                Text(title).font(.system(size:25,weight:.semibold,design:.rounded)).multilineTextAlignment(.center)
                Text(message).font(.callout).foregroundStyle(.secondary).multilineTextAlignment(.center).fixedSize(horizontal:false,vertical:true)
                actions()
            }
            .frame(width:max(0,min(440,space.size.width-48))).padding(space.size.height<300 ? 12:24)
            .frame(width:space.size.width,height:space.size.height,alignment:.center)
        }
    }
}
extension WorkspaceEmptyState where Actions==EmptyView {
    init(title:String,icon:String,message:String){self.title=title;self.icon=icon;self.message=message;self.actions={EmptyView()}}
}
